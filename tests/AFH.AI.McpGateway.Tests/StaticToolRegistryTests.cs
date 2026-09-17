using AFH.AI.McpGateway.Application.Services;

namespace AFH.AI.McpGateway.Tests;

public sealed class StaticToolRegistryTests
{
    [Fact]
    public void GetTools_ContainsBookingLifecycleAsFirstRealDownstreamCandidate()
    {
        var registry = new StaticToolRegistry();

        var tool = registry.GetTools().Single(candidate => candidate.Name == "booking.get_lifecycle");

        Assert.Equal("Booking", tool.OwnerService);
        Assert.Equal("GET", tool.Endpoint.Method);
        Assert.Equal("/v1/bookings/{bookingId}/lifecycle", tool.Endpoint.RouteTemplate);
        Assert.Contains("booking.read", tool.RequiredPermissions);
    }

    [Fact]
    public void GetTools_ContainsBookingSearchTools()
    {
        var registry = new StaticToolRegistry();

        var myBookings = registry.GetTools().Single(candidate => candidate.Name == "booking.get_my_bookings");
        var search = registry.GetTools().Single(candidate => candidate.Name == "booking.search");
        var details = registry.GetTools().Single(candidate => candidate.Name == "booking.get_details");

        Assert.Equal("GET", myBookings.Endpoint.Method);
        Assert.Equal("/v1/admin/bookings", myBookings.Endpoint.RouteTemplate);
        Assert.Contains("booking.read", myBookings.RequiredPermissions);
        Assert.Contains(myBookings.Parameters, parameter => parameter.Name == "from" && !parameter.Required);
        Assert.Equal("/v1/admin/bookings", search.Endpoint.RouteTemplate);
        Assert.Contains(search.Parameters, parameter => parameter.Name == "adviserId" && !parameter.Required);
        Assert.Equal("/v1/bookings/{bookingId}", details.Endpoint.RouteTemplate);
    }

    [Fact]
    public void GetTools_ContainsAumPhaseOneTools()
    {
        var registry = new StaticToolRegistry();

        var tools = registry.GetTools();
        var clients = tools.Single(candidate => candidate.Name == "aum.get_my_clients");
        var highValue = tools.Single(candidate => candidate.Name == "aum.find_my_high_value_clients");
        var missingReview = tools.Single(candidate => candidate.Name == "aum.find_my_clients_missing_annual_review");

        Assert.Equal("Adviser Insights", clients.OwnerService);
        Assert.Equal("GET", clients.Endpoint.Method);
        Assert.Equal("/v1/me/clients", clients.Endpoint.RouteTemplate);
        Assert.Equal("Services:AdviserInsights:BaseUrl", clients.Endpoint.ServiceBaseUrlSetting);
        Assert.Contains("aum.read", clients.RequiredPermissions);
        Assert.Contains(clients.Parameters, parameter => parameter.Name == "pageSize" && !parameter.Required);
        Assert.Equal("/v1/me/clients/highest-policy-value", highValue.Endpoint.RouteTemplate);
        Assert.Equal("/v1/me/clients/missing-annual-review", missingReview.Endpoint.RouteTemplate);
    }

    [Fact]
    public void GetTools_ContainsSnowflakeAgentTool()
    {
        var registry = new StaticToolRegistry();

        var tool = registry.GetTools().Single(candidate => candidate.Name == "snowflake.ask_agent");

        Assert.Equal("Adviser Insights", tool.OwnerService);
        Assert.Equal("POST", tool.Endpoint.Method);
        Assert.Equal("/v1/insights/ask", tool.Endpoint.RouteTemplate);
        Assert.Equal("Services:AdviserInsights:BaseUrl", tool.Endpoint.ServiceBaseUrlSetting);
        Assert.Contains("aum.read", tool.RequiredPermissions);
        Assert.Contains(tool.Parameters, parameter => parameter.Name == "question" && parameter.Required);
    }
}
