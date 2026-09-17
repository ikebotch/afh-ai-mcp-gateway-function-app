using System.Text.Json;
using AFH.AI.McpGateway.Application.Abstractions;
using AFH.AI.McpGateway.Application.Models;
using AFH.AI.McpGateway.Application.Services;
using AFH.Common.AI.Actor;
using AFH.Common.AI.Audit;
using AFH.Common.AI.Tools;
using AFH.Common.Mcp.Protocol;
using Moq;

namespace AFH.AI.McpGateway.Tests;

public sealed class ToolInvocationServiceTests
{
    [Fact]
    public async Task InvokeAsync_WhenPermissionMissing_ReturnsForbiddenBeforeDownstreamCall()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "codex", null, "corr-1", []);
        var request = new McpToolInvocationRequest(JsonSerializer.SerializeToElement(new { bookingId = "booking-1" }), null);

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "booking.get_lifecycle" &&
                    entry.Outcome == "Failed" &&
                    entry.DownstreamService == "Booking" &&
                    entry.DownstreamTarget == null &&
                    entry.ExecutionMode == "Blocked" &&
                    entry.StatusCode == 403 &&
                    entry.FailureReason == "Tool 'booking.get_lifecycle' requires permission 'booking.read'."),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.InvokeAsync("booking.get_lifecycle", request, actor, CancellationToken.None));

        downstream.VerifyNoOtherCalls();
        audit.VerifyAll();
    }

    [Fact]
    public async Task InvokeAsync_WhenWriteToolWithoutAiWrite_AuditsBlockedCall()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "codex", null, "corr-1", ["devops.workitems.write"]);
        var request = new McpToolInvocationRequest(JsonSerializer.SerializeToElement(new { title = "Story" }), null);

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "devops.create_user_story" &&
                    entry.Outcome == "Failed" &&
                    entry.DownstreamService == "DevOps Integration" &&
                    entry.ExecutionMode == "Blocked" &&
                    entry.StatusCode == 403 &&
                    entry.FailureReason == "Tool 'devops.create_user_story' requires write access."),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.InvokeAsync("devops.create_user_story", request, actor, CancellationToken.None));

        downstream.VerifyNoOtherCalls();
        audit.VerifyAll();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorized_AuditsAndReturnsDownstreamResponse()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "codex", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { bookingId = "booking-1" });
        var request = new McpToolInvocationRequest(arguments, null);

        downstream
            .Setup(client => client.InvokeAsync(
                It.Is<AiToolDescriptor>(tool => tool.Name == "booking.get_lifecycle"),
                arguments,
                actor,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolDownstreamResult(
                200,
                JsonSerializer.SerializeToElement(new { bookingId = "booking-1", lifecycleState = "Booked" }),
                "http://localhost:7071/api/v1/bookings/booking-1/lifecycle",
                false));

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "booking.get_lifecycle" &&
                    entry.Outcome == "Succeeded" &&
                    entry.CorrelationId == "corr-1" &&
                    entry.DownstreamService == "Booking" &&
                    entry.ExecutionMode == "Real" &&
                    entry.FailureReason == null),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await service.InvokeAsync("booking.get_lifecycle", request, actor, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("booking.get_lifecycle", response.ToolName);
        downstream.VerifyAll();
        audit.VerifyAll();
    }

    [Fact]
    public async Task InvokeAsync_WhenDryRunResponse_AuditsDryRunExecutionMode()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "codex", null, "corr-1", ["devops.sprints.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { project = "AFH" });
        var request = new McpToolInvocationRequest(arguments, null);

        downstream
            .Setup(client => client.InvokeAsync(
                It.Is<AiToolDescriptor>(tool => tool.Name == "devops.get_current_sprint"),
                arguments,
                actor,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolDownstreamResult(
                202,
                JsonSerializer.SerializeToElement(new { dryRun = true }),
                "http://localhost:7076/api/internal/devops/v1/sprints/current",
                true));

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "devops.get_current_sprint" &&
                    entry.Outcome == "Succeeded" &&
                    entry.DownstreamService == "DevOps Integration" &&
                    entry.ExecutionMode == "DryRun" &&
                    entry.StatusCode == 202 &&
                    entry.FailureReason == null),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await service.InvokeAsync("devops.get_current_sprint", request, actor, CancellationToken.None);

        Assert.Equal(202, response.StatusCode);
        downstream.VerifyAll();
        audit.VerifyAll();
    }

    [Fact]
    public async Task InvokeAsync_WhenDownstreamFails_AuditsFailureReason()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "codex", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { bookingId = "booking-1" });
        var request = new McpToolInvocationRequest(arguments, null);

        downstream
            .Setup(client => client.InvokeAsync(
                It.Is<AiToolDescriptor>(tool => tool.Name == "booking.get_lifecycle"),
                arguments,
                actor,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolDownstreamResult(
                500,
                JsonSerializer.SerializeToElement(new { error = "database unavailable" }),
                "http://localhost:7071/api/v1/bookings/booking-1/lifecycle",
                false));

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "booking.get_lifecycle" &&
                    entry.Outcome == "Failed" &&
                    entry.ExecutionMode == "Real" &&
                    entry.StatusCode == 500 &&
                    entry.FailureReason == "Downstream returned HTTP 500. database unavailable"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await service.InvokeAsync("booking.get_lifecycle", request, actor, CancellationToken.None);

        Assert.Equal(500, response.StatusCode);
        downstream.VerifyAll();
        audit.VerifyAll();
    }

    [Fact]
    public async Task InvokeAsync_WhenDownstreamThrows_AuditsExceptionAndReturnsToolError()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["aum.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { question = "Show advisers." });
        var request = new McpToolInvocationRequest(arguments, null);

        downstream
            .Setup(client => client.InvokeAsync(
                It.Is<AiToolDescriptor>(tool => tool.Name == "snowflake.ask_agent"),
                arguments,
                actor,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Adviser Insights downstream is unavailable."));

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "snowflake.ask_agent" &&
                    entry.Outcome == "Failed" &&
                    entry.DownstreamService == "Adviser Insights" &&
                    entry.DownstreamTarget == null &&
                    entry.ExecutionMode == "Real" &&
                    entry.StatusCode == 500 &&
                    entry.FailureReason == "Adviser Insights downstream is unavailable." &&
                    entry.ResponsePayload != null &&
                    entry.ResponsePayload.Contains("Downstream tool invocation failed.", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await service.InvokeAsync("snowflake.ask_agent", request, actor, CancellationToken.None);

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("snowflake.ask_agent", response.ToolName);
        Assert.Equal("Downstream tool invocation failed.", response.Content.GetProperty("error").GetString());
        downstream.VerifyAll();
        audit.VerifyAll();
    }

    [Fact]
    public async Task InvokeAsync_WhenDownstreamReturnsErrorBody_AuditsFailureReasonWithErrorDetails()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { pageSize = 10 });
        var request = new McpToolInvocationRequest(arguments, null);

        downstream
            .Setup(client => client.InvokeAsync(
                It.Is<AiToolDescriptor>(tool => tool.Name == "booking.get_my_bookings"),
                arguments,
                actor,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolDownstreamResult(
                401,
                JsonSerializer.SerializeToElement(new
                {
                    code = "Unauthorized",
                    message = "Authenticated domain user identity is required."
                }),
                "http://localhost:7071/api/v1/admin/bookings?pageSize=10",
                false));

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "booking.get_my_bookings" &&
                    entry.Outcome == "Failed" &&
                    entry.StatusCode == 401 &&
                    entry.FailureReason == "Downstream returned HTTP 401. Unauthorized: Authenticated domain user identity is required."),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await service.InvokeAsync("booking.get_my_bookings", request, actor, CancellationToken.None);

        Assert.Equal(401, response.StatusCode);
        downstream.VerifyAll();
        audit.VerifyAll();
    }

    [Fact]
    public async Task InvokeAsync_WhenDownstreamErrorBodyIsLong_TruncatesFailureReason()
    {
        var registry = new StaticToolRegistry();
        var downstream = new Mock<IToolDownstreamClient>(MockBehavior.Strict);
        var audit = new Mock<IAiAuditSink>(MockBehavior.Strict);
        var service = new ToolInvocationService(registry, downstream.Object, audit.Object);
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { pageSize = 10 });
        var request = new McpToolInvocationRequest(arguments, null);

        downstream
            .Setup(client => client.InvokeAsync(
                It.Is<AiToolDescriptor>(tool => tool.Name == "booking.get_my_bookings"),
                arguments,
                actor,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolDownstreamResult(
                500,
                JsonSerializer.SerializeToElement(new { message = new string('x', 3000) }),
                "http://localhost:7071/api/v1/admin/bookings?pageSize=10",
                false));

        audit
            .Setup(sink => sink.WriteAsync(
                It.Is<AiAuditEvent>(entry =>
                    entry.ToolName == "booking.get_my_bookings" &&
                    entry.FailureReason != null &&
                    entry.FailureReason.Length == 2048 &&
                    entry.FailureReason.StartsWith("Downstream returned HTTP 500. ", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await service.InvokeAsync("booking.get_my_bookings", request, actor, CancellationToken.None);

        Assert.Equal(500, response.StatusCode);
        downstream.VerifyAll();
        audit.VerifyAll();
    }
}
