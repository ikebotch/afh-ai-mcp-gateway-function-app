namespace AFH.AI.McpGateway.Infrastructure.Options;

/// <summary>
/// Configures the MCP Gateway runtime.
/// </summary>
public sealed class McpGatewayOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "McpGateway";

    /// <summary>
    /// Gets or sets the optional shared API key expected from AI clients.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets caller authentication configuration.
    /// </summary>
    public McpGatewayAuthenticationOptions Authentication { get; set; } = new();

    /// <summary>
    /// Gets or sets whether downstream calls should be replaced with dry-run responses.
    /// </summary>
    public bool DryRunDownstreamCalls { get; set; } = true;

    /// <summary>
    /// Gets or sets tools that should call real downstream services even when global dry-run is enabled.
    /// </summary>
    public string[] RealDownstreamTools { get; set; } =
    [
        "aum.get_my_adviser_profile",
        "aum.get_my_team_advisers",
        "aum.get_my_clients",
        "aum.get_my_policies",
        "aum.get_my_aum_summary",
        "aum.find_my_high_value_clients",
        "aum.find_my_clients_missing_annual_review",
        "booking.get_lifecycle",
        "booking.find_availability",
        "booking.get_my_bookings",
        "booking.search",
        "booking.get_details"
    ];

    /// <summary>
    /// Gets or sets the internal API key forwarded to downstream AFH services.
    /// </summary>
    public string? DownstreamApiKey { get; set; }

    /// <summary>
    /// Gets or sets durable audit configuration.
    /// </summary>
    public McpGatewayAuditOptions Audit { get; set; } = new();
}

/// <summary>
/// Configures MCP Gateway audit persistence.
/// </summary>
public sealed class McpGatewayAuditOptions
{
    /// <summary>
    /// Gets or sets the audit provider. Supported values are Logging and TableStorage.
    /// </summary>
    public string Provider { get; set; } = "Logging";

    /// <summary>
    /// Gets or sets the Azure Table Storage connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Azure Table Storage table name.
    /// </summary>
    public string TableName { get; set; } = "AiToolAudit";
}

/// <summary>
/// Configures Microsoft Entra access-token validation for MCP callers.
/// </summary>
public sealed class McpGatewayAuthenticationOptions
{
    /// <summary>
    /// Gets or sets whether bearer-token validation is required.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the Microsoft Entra tenant identifier.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the token issuer authority. Defaults to the tenant v2.0 endpoint when TenantId is set.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Gets or sets the expected access-token audience, for example api://afh-ai-mcp-gateway.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets or sets the accepted clock skew in minutes.
    /// </summary>
    public int ClockSkewMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether safe, non-secret JWT claim diagnostics are written to logs.
    /// </summary>
    public bool LogTokenDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets whether safe, redacted HTTP request diagnostics are written to logs.
    /// </summary>
    public bool LogRequestDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets whether the raw Authorization header is written to request diagnostics. Use only temporarily.
    /// </summary>
    public bool LogSensitiveAuthorizationHeader { get; set; }

    /// <summary>
    /// Gets or sets scope-to-internal-permission mappings.
    /// </summary>
    public Dictionary<string, string[]> ScopePermissionMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mcp.tools.read"] = ["ai.read"],
        ["mcp.tools.write"] = ["ai.write"],
        ["mcp.tools.booking.read"] = ["booking.read", "availability.read"],
        ["mcp.tools.calendar.read"] = ["calendar.read"],
        ["mcp.tools.notification.read"] = ["notification.read"],
        ["mcp.tools.notification.write"] = ["ai.write", "notification.write"],
        ["mcp.tools.location.read"] = ["location.read"],
        ["mcp.tools.client.read"] = ["client.read"],
        ["mcp.tools.aum.read"] = ["aum.read"],
        ["mcp.tools.devops.read"] = ["devops.sprints.read"],
        ["mcp.tools.devops.write"] = ["ai.write", "devops.workitems.write"],
        ["access_as_user"] = ["ai.read", "booking.read", "availability.read", "aum.read"]
    };

    /// <summary>
    /// Gets or sets app-role-to-internal-permission mappings.
    /// </summary>
    public Dictionary<string, string[]> RolePermissionMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
