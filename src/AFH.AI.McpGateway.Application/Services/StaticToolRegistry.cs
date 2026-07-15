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
