namespace AFH.AI.McpGateway.Infrastructure.Options;

/// <summary>
/// Configures downstream AFH service base URLs.
/// </summary>
public sealed class DownstreamServiceOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Services";

    /// <summary>Gets or sets service configuration by service key.</summary>
    public Dictionary<string, DownstreamServiceEndpointOptions> Endpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Configures a single downstream service endpoint.
/// </summary>
public sealed class DownstreamServiceEndpointOptions
{
    /// <summary>Gets or sets the downstream service base URL.</summary>
    public string? BaseUrl { get; set; }
}
