namespace AFH.AI.McpGateway.Infrastructure.Security;

/// <summary>
/// Carries caller authentication inputs extracted from an MCP HTTP request.
/// </summary>
public sealed record McpGatewayAuthenticationRequest(
    string? AuthorizationHeader,
    string? GatewayApiKey,
    string CorrelationId,
    string? LegacyActorId,
    string? LegacyAgentId,
    string? LegacyTenantId,
    IReadOnlyCollection<string> LegacyPermissions,
    string? IdempotencyKey,
    string? ApprovalId,
    bool IsApproved);
