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
}
