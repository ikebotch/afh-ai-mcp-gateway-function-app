using System.Net;
using System.Text;
using System.Text.Json;
using AFH.AI.McpGateway.Application.Services;
using AFH.AI.McpGateway.Infrastructure.Http;
using AFH.AI.McpGateway.Infrastructure.Options;
using AFH.Common.AI.Actor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AFH.AI.McpGateway.Tests;

public sealed class HttpToolDownstreamClientTests
{
    [Fact]
    public async Task InvokeAsync_ForDefaultRealAumTool_CallsAdviserInsightsEndpoint()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("aum.get_my_clients", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true,"data":[]}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:AdviserInsights:BaseUrl"] = "https://adviser-insights.test"
            },
            new McpGatewayOptions());
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["aum.read"], "Bearer delegated-token");
        var arguments = JsonSerializer.SerializeToElement(new { pageSize = 10 });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.False(result.IsDryRun);
        Assert.Equal("https://adviser-insights.test/api/v1/me/clients?pageSize=10", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer delegated-token", handler.Request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ForDefaultRealBookingSearchTool_CallsRealBookingEndpoint()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("booking.get_my_bookings", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Booking:BaseUrl"] = "http://booking-service.test/api"
            },
            new McpGatewayOptions());
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { pageSize = 10 });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.False(result.IsDryRun);
        Assert.Equal("http://booking-service.test/api/v1/admin/bookings?pageSize=10", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenServiceBaseUrlHasNoPath_AddsAzureFunctionsApiPrefix()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("booking.get_my_bookings", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Booking:BaseUrl"] = "https://booking-service-dev.azurewebsites.net"
            },
            new McpGatewayOptions());
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { pageSize = 10 });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(
            "https://booking-service-dev.azurewebsites.net/api/v1/admin/bookings?pageSize=10",
            handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task InvokeAsync_WhenServiceBaseUrlAlreadyHasApiPath_DoesNotDuplicateApiPrefix()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("booking.get_my_bookings", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"items":[]}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Booking:BaseUrl"] = "https://booking-service-dev.azurewebsites.net/api"
            },
            new McpGatewayOptions());
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { pageSize = 10 });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(
            "https://booking-service-dev.azurewebsites.net/api/v1/admin/bookings?pageSize=10",
            handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ForBookingLifecycleRealTool_CallsRealBookingEndpoint()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("booking.get_lifecycle", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"bookingId":"booking-1","lifecycleState":"Booked"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Booking:BaseUrl"] = "http://booking-service.test/api"
            },
            new McpGatewayOptions
            {
                DryRunDownstreamCalls = true,
                RealDownstreamTools = ["booking.get_lifecycle"],
                DownstreamApiKey = "internal-key"
            });
        var actor = new AiActorContext(
            "user-1",
            "codex",
            null,
            "corr-1",
            ["booking.read"],
            "Bearer delegated-token",
            "idem-1",
            "approval-1",
            true);
        var arguments = JsonSerializer.SerializeToElement(new { bookingId = "booking-1" });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("http://booking-service.test/api/v1/bookings/booking-1/lifecycle", handler.Request!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Request.Method);
        Assert.True(handler.Request.Headers.TryGetValues("x-correlation-id", out var correlationValues));
        Assert.Contains("corr-1", correlationValues);
        Assert.True(handler.Request.Headers.TryGetValues("x-afh-ai-actor-id", out var actorValues));
        Assert.Contains("user-1", actorValues);
        Assert.True(handler.Request.Headers.TryGetValues("x-afh-ai-agent-id", out var agentValues));
        Assert.Contains("codex", agentValues);
        Assert.True(handler.Request.Headers.TryGetValues("x-afh-internal-api-key", out var keyValues));
        Assert.Contains("internal-key", keyValues);
        Assert.True(handler.Request.Headers.TryGetValues("x-idempotency-key", out var idempotencyValues));
        Assert.Contains("idem-1", idempotencyValues);
        Assert.True(handler.Request.Headers.TryGetValues("x-afh-ai-approval-id", out var approvalValues));
        Assert.Contains("approval-1", approvalValues);
        Assert.True(handler.Request.Headers.TryGetValues("x-afh-ai-approved", out var approvedValues));
        Assert.Contains("True", approvedValues);
        Assert.Equal("Bearer delegated-token", handler.Request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ForNonRealTool_WhenGlobalDryRun_ReturnsDryRunTargetWithoutHttpCall()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("calendar.get_schedule", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Calendar:BaseUrl"] = "http://calendar-service.test/api"
            },
            new McpGatewayOptions { DryRunDownstreamCalls = true, RealDownstreamTools = ["booking.get_lifecycle"] });
        var actor = new AiActorContext("user-1", "codex", null, "corr-1", ["calendar.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { userId = "adviser-1" });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(202, result.StatusCode);
        Assert.Null(handler.Request);
        Assert.Equal("http://calendar-service.test/api/v1/calendar/users/adviser-1/schedule", result.Target);
        Assert.True(result.Content.GetProperty("dryRun").GetBoolean());
    }

    [Fact]
    public async Task InvokeAsync_ForOtherBookingTool_WhenOnlyLifecycleIsReal_ReturnsDryRunTarget()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("booking.find_availability", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Booking:BaseUrl"] = "http://booking-service.test/api"
            },
            new McpGatewayOptions { DryRunDownstreamCalls = true, RealDownstreamTools = ["booking.get_lifecycle"] });
        var actor = new AiActorContext("user-1", "codex", null, "corr-1", ["booking.read", "availability.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { transactionId = "txn-1" });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(202, result.StatusCode);
        Assert.Null(handler.Request);
        Assert.Equal("http://booking-service.test/api/v2/transactions/txn-1/availability", result.Target);
        Assert.True(result.Content.GetProperty("dryRun").GetBoolean());
    }

    [Fact]
    public async Task InvokeAsync_ForBookingSearchRealTool_AppendsFiltersAsQueryString()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("booking.search", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"items":[],"page":1,"pageSize":25,"totalItems":0,"totalPages":0}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Booking:BaseUrl"] = "http://booking-service.test/api"
            },
            new McpGatewayOptions
            {
                DryRunDownstreamCalls = true,
                RealDownstreamTools = ["booking.search"]
            });
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["booking.read"], "Bearer delegated-token");
        var arguments = JsonSerializer.SerializeToElement(new
        {
            status = "Confirmed",
            from = "2026-07-22T00:00:00Z",
            to = "2026-07-29T23:59:59Z",
            pageSize = 10
        });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(
            "http://booking-service.test/api/v1/admin/bookings?status=Confirmed&from=2026-07-22T00%3A00%3A00Z&to=2026-07-29T23%3A59%3A59Z&pageSize=10",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Request.Method);
        Assert.Equal("Bearer delegated-token", handler.Request.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ForBookingDetailsRealTool_DoesNotAppendPathParameterAsQueryString()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("booking.get_details", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"bookingId":"booking-1"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:Booking:BaseUrl"] = "http://booking-service.test/api"
            },
            new McpGatewayOptions
            {
                DryRunDownstreamCalls = true,
                RealDownstreamTools = ["booking.get_details"]
            });
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["booking.read"]);
        var arguments = JsonSerializer.SerializeToElement(new { bookingId = "booking-1" });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("http://booking-service.test/api/v1/bookings/booking-1", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task InvokeAsync_ForSnowflakeAgentTool_CallsAdviserInsightsWithDelegatedToken()
    {
        var registry = new StaticToolRegistry();
        Assert.True(registry.TryGetTool("snowflake.ask_agent", out var tool));
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"message":"answer"}""", Encoding.UTF8, "application/json")
        });
        var client = CreateClient(
            handler,
            new Dictionary<string, string?>
            {
                ["Services:AdviserInsights:BaseUrl"] = "https://adviser-insights.test"
            },
            new McpGatewayOptions());
        var actor = new AiActorContext("user-1", "copilot", null, "corr-1", ["aum.read"], "Bearer entra-token");
        var arguments = JsonSerializer.SerializeToElement(new { question = "What is the average rating for adviser X?" });

        var result = await client.InvokeAsync(tool!, arguments, actor, CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.False(result.IsDryRun);
        Assert.Equal("https://adviser-insights.test/api/v1/insights/ask", handler.Request!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("Bearer entra-token", handler.Request.Headers.Authorization?.ToString());

        Assert.NotNull(handler.RequestBody);
        var body = handler.RequestBody!;
        using var json = JsonDocument.Parse(body);
        Assert.Equal("What is the average rating for adviser X?", json.RootElement.GetProperty("question").GetString());
    }

    private static HttpToolDownstreamClient CreateClient(
        HttpMessageHandler handler,
        IReadOnlyDictionary<string, string?> configurationValues,
        McpGatewayOptions options)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        return new HttpToolDownstreamClient(
            new StaticHttpClientFactory(new HttpClient(handler)),
            configuration,
            Options.Create(options));
    }

    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return response;
        }
    }
}
