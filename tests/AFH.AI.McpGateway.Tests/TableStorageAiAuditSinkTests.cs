using AFH.AI.McpGateway.Infrastructure.Audit;
using AFH.Common.AI.Audit;

namespace AFH.AI.McpGateway.Tests;

public sealed class TableStorageAiAuditSinkTests
{
    [Fact]
    public void ToEntity_MapsAuditEventToDurableTableEntity()
    {
        var auditEvent = new AiAuditEvent(
            "event-1",
            new DateTimeOffset(2026, 07, 06, 12, 30, 00, TimeSpan.Zero),
            "user-1",
            "codex",
            "booking.get_lifecycle",
            "Succeeded",
            "corr-1",
            "Booking",
            "http://localhost:7071/api/v1/bookings/booking-1/lifecycle",
            "Real",
            200,
            123,
            null);

        var entity = TableStorageAiAuditSink.ToEntity(auditEvent);

        Assert.Equal("20260706|Booking", entity.PartitionKey);
        Assert.EndsWith("|event-1", entity.RowKey, StringComparison.Ordinal);
        Assert.Equal("event-1", entity["EventId"]);
        Assert.Equal("user-1", entity["ActorId"]);
        Assert.Equal("codex", entity["AgentId"]);
        Assert.Equal("booking.get_lifecycle", entity["ToolName"]);
        Assert.Equal("Succeeded", entity["Outcome"]);
        Assert.Equal("corr-1", entity["CorrelationId"]);
        Assert.Equal("Booking", entity["DownstreamService"]);
        Assert.Equal("http://localhost:7071/api/v1/bookings/booking-1/lifecycle", entity["DownstreamTarget"]);
        Assert.Equal("Real", entity["ExecutionMode"]);
        Assert.Equal(200, entity["StatusCode"]);
        Assert.Equal(123L, entity["DurationMs"]);
        Assert.False(entity.ContainsKey("FailureReason"));
    }

    [Fact]
    public void ToEntity_WhenFailure_MapsFailureReason()
    {
        var auditEvent = new AiAuditEvent(
            "event-2",
            new DateTimeOffset(2026, 07, 06, 12, 30, 00, TimeSpan.Zero),
            "user-1",
            "codex",
            "devops.create_user_story",
            "Failed",
            "corr-1",
            "DevOps Integration",
            null,
            "DryRun",
            500,
            42,
            "Downstream returned HTTP 500.");

        var entity = TableStorageAiAuditSink.ToEntity(auditEvent);

        Assert.Equal("20260706|DevOps Integration", entity.PartitionKey);
        Assert.Equal("DryRun", entity["ExecutionMode"]);
        Assert.Equal("Downstream returned HTTP 500.", entity["FailureReason"]);
        Assert.False(entity.ContainsKey("DownstreamTarget"));
    }
}
