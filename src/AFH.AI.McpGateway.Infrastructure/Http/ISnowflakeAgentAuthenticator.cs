using System.Net.Http;

namespace AFH.AI.McpGateway.Infrastructure.Http;

/// <summary>
/// Applies authentication headers for Snowflake Cortex agent requests.
/// </summary>
public interface ISnowflakeAgentAuthenticator
{
    /// <summary>
    /// Applies Snowflake authentication headers to the supplied request.
    /// </summary>
    /// <param name="request">The outgoing Snowflake request.</param>
    void Apply(HttpRequestMessage request);
}
