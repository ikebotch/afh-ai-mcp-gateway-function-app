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
}
