using AFH.AI.McpGateway.Application.Abstractions;
using AFH.Common.AI.Tools;

namespace AFH.AI.McpGateway.Application.Services;

/// <summary>
/// Provides the Phase 1 static tool registry before service-published manifests are enabled.
/// </summary>
public sealed class StaticToolRegistry : IToolRegistry
{
    private static readonly IReadOnlyCollection<AiToolDescriptor> Tools =
    [
        new(
            "aum.get_my_adviser_profile",
            "Adviser Insights",
            "Reads the signed-in adviser profile from the Adviser Insights service.",
            AiToolSideEffectLevel.ReadOnly,
            ["aum.read"],
            new("GET", "/v1/me/adviser", "Services:AdviserInsights:BaseUrl"),
            []),
        new(
            "aum.get_my_team_advisers",
            "Adviser Insights",
            "Reads advisers managed by the signed-in manager.",
            AiToolSideEffectLevel.ReadOnly,
            ["aum.read"],
            new("GET", "/v1/me/team/advisers", "Services:AdviserInsights:BaseUrl"),
            []),
        new(
            "aum.get_my_clients",
            "Adviser Insights",
            "Reads clients in the signed-in user's adviser or manager scope.",
            AiToolSideEffectLevel.ReadOnly,
            ["aum.read"],
            new("GET", "/v1/me/clients", "Services:AdviserInsights:BaseUrl"),
            [new("pageSize", "integer", false, "The maximum number of clients to return.")]),
        new(
            "aum.get_my_policies",
            "Adviser Insights",
            "Reads policies in the signed-in user's adviser or manager scope.",
            AiToolSideEffectLevel.ReadOnly,
            ["aum.read"],
            new("GET", "/v1/me/policies", "Services:AdviserInsights:BaseUrl"),
            [new("pageSize", "integer", false, "The maximum number of policies to return.")]),
        new(
            "aum.get_my_aum_summary",
            "Adviser Insights",
            "Reads an AUM summary for the signed-in user's adviser or manager scope.",
            AiToolSideEffectLevel.ReadOnly,
            ["aum.read"],
            new("GET", "/v1/me/aum-summary", "Services:AdviserInsights:BaseUrl"),
            []),
        new(
            "aum.find_my_high_value_clients",
            "Adviser Insights",
            "Finds clients with the highest policy or AUM value in the signed-in user's scope.",
            AiToolSideEffectLevel.ReadOnly,
            ["aum.read"],
            new("GET", "/v1/me/clients/highest-policy-value", "Services:AdviserInsights:BaseUrl"),
            [new("pageSize", "integer", false, "The maximum number of clients to return.")]),
        new(
            "aum.find_my_clients_missing_annual_review",
            "Adviser Insights",
            "Finds clients without an annual policy review in the last 12 months.",
            AiToolSideEffectLevel.ReadOnly,
            ["aum.read"],
            new("GET", "/v1/me/clients/missing-annual-review", "Services:AdviserInsights:BaseUrl"),
            [new("pageSize", "integer", false, "The maximum number of clients to return.")]),
        new(
            "booking.get_my_bookings",
            "Booking",
            "Searches bookings visible to the signed-in user, using the Booking service access scope.",
            AiToolSideEffectLevel.ReadOnly,
            ["booking.read"],
            new("GET", "/v1/admin/bookings", "Services:Booking:BaseUrl"),
            [
                new("search", "string", false, "Optional free-text search across reference, client, adviser, meeting type and location fields."),
                new("status", "string", false, "Optional booking status filter: Active, Confirmed, Released, Cancelled or Expired."),
                new("from", "string", false, "Optional UTC lower bound for booking start, for example 2026-07-22T00:00:00Z."),
                new("to", "string", false, "Optional UTC upper bound for booking start, for example 2026-07-29T23:59:59Z."),
                new("page", "integer", false, "Optional 1-based page number. Defaults to 1."),
                new("pageSize", "integer", false, "Optional page size from 1 to 100. Defaults to 25.")
            ]),
        new(
            "booking.search",
            "Booking",
            "Searches bookings with filters. Results remain limited by the signed-in user's Booking service permissions.",
            AiToolSideEffectLevel.ReadOnly,
            ["booking.read"],
            new("GET", "/v1/admin/bookings", "Services:Booking:BaseUrl"),
            [
                new("search", "string", false, "Optional free-text search across reference, client, adviser, meeting type and location fields."),
                new("bookingId", "string", false, "Optional booking identifier filter."),
                new("transactionId", "string", false, "Optional booking transaction identifier filter."),
                new("transactionRef", "string", false, "Optional external transaction or client reference filter."),
                new("status", "string", false, "Optional booking status filter: Active, Confirmed, Released, Cancelled or Expired."),
                new("adviserId", "string", false, "Optional adviser identifier filter."),
                new("adviserName", "string", false, "Optional adviser name filter."),
                new("clientRef", "string", false, "Optional client reference filter."),
                new("locationRef", "string", false, "Optional location reference filter."),
                new("meetingType", "string", false, "Optional meeting type filter."),
                new("mode", "string", false, "Optional booking mode filter: Online, InPerson or Phone."),
                new("from", "string", false, "Optional UTC lower bound for booking start, for example 2026-07-22T00:00:00Z."),
                new("to", "string", false, "Optional UTC upper bound for booking start, for example 2026-07-29T23:59:59Z."),
                new("page", "integer", false, "Optional 1-based page number. Defaults to 1."),
                new("pageSize", "integer", false, "Optional page size from 1 to 100. Defaults to 25.")
            ]),
        new(
            "booking.get_details",
            "Booking",
            "Reads booking details for a single booking.",
            AiToolSideEffectLevel.ReadOnly,
            ["booking.read"],
            new("GET", "/v1/bookings/{bookingId}", "Services:Booking:BaseUrl"),
            [new("bookingId", "string", true, "The booking identifier.")]),
        new(
            "booking.get_lifecycle",
            "Booking",
            "Reads the lifecycle state and audit history for a booking.",
            AiToolSideEffectLevel.ReadOnly,
            ["booking.read"],
            new("GET", "/v1/bookings/{bookingId}/lifecycle", "Services:Booking:BaseUrl"),
            [new("bookingId", "string", true, "The booking identifier.")]),
        new(
            "booking.find_availability",
            "Booking",
            "Finds appointment availability for a booking transaction.",
            AiToolSideEffectLevel.ReadOnly,
            ["booking.read", "availability.read"],
            new("POST", "/v2/transactions/{transactionId}/availability", "Services:Booking:BaseUrl"),
            [new("transactionId", "string", true, "The booking transaction identifier.")]),
        new(
            "calendar.get_schedule",
            "Calendar",
            "Reads schedule availability for a user.",
            AiToolSideEffectLevel.ReadOnly,
            ["calendar.read"],
            new("GET", "/v1/calendar/users/{userId}/schedule", "Services:Calendar:BaseUrl"),
            [new("userId", "string", true, "The calendar user identifier.")]),
        new(
            "notification.get_request",
            "Notification",
            "Reads a notification request and delivery status.",
            AiToolSideEffectLevel.ReadOnly,
            ["notification.read"],
            new("GET", "/v1/notifications/requests/{id}", "Services:Notification:BaseUrl"),
            [new("id", "string", true, "The notification request identifier.")]),
        new(
            "location.route_time",
            "Location",
            "Calculates route time for a proposed appointment location.",
            AiToolSideEffectLevel.ReadOnly,
            ["location.read"],
            new("POST", "/v1/location/route-time", "Services:Location:BaseUrl"),
            []),
        new(
            "client.get_summary",
            "Client Integration",
            "Reads a client summary for AI-assisted support and planning.",
            AiToolSideEffectLevel.ReadOnly,
            ["client.read"],
            new("GET", "/internal/client/v1/clients/{clientId}/summary", "Services:ClientIntegration:BaseUrl"),
            [new("clientId", "string", true, "The client identifier.")]),
        new(
            "devops.create_user_story",
            "DevOps Integration",
            "Creates or dry-runs an Azure DevOps user story from structured requirements.",
            AiToolSideEffectLevel.Write,
            ["devops.workitems.write"],
            new("POST", "/internal/devops/v1/work-items/user-stories", "Services:DevOps:BaseUrl"),
            []),
        new(
            "devops.get_current_sprint",
            "DevOps Integration",
            "Reads current Azure DevOps sprint metadata.",
            AiToolSideEffectLevel.ReadOnly,
            ["devops.sprints.read"],
            new("GET", "/internal/devops/v1/sprints/current", "Services:DevOps:BaseUrl"),
            [])
    ];

    /// <inheritdoc />
    public IReadOnlyCollection<AiToolDescriptor> GetTools() => Tools;

    /// <inheritdoc />
    public bool TryGetTool(string toolName, out AiToolDescriptor? tool)
    {
        tool = Tools.FirstOrDefault(candidate => string.Equals(candidate.Name, toolName, StringComparison.OrdinalIgnoreCase));
        return tool is not null;
    }
}
