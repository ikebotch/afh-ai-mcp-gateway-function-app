using AFH.AI.McpGateway.Infrastructure.Options;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AFH.AI.McpGateway.Function.Http;

/// <summary>
/// Writes safe, redacted diagnostics for inbound MCP HTTP requests.
/// </summary>
public static class McpRequestDiagnostics
{
    /// <summary>
    /// Logs a redacted request summary and curl-style shape when diagnostics are enabled.
    /// </summary>
    /// <param name="request">The incoming HTTP request.</param>
    /// <param name="options">The authentication options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="endpointName">The logical endpoint name.</param>
    public static void LogIfEnabled(
        HttpRequestData request,
        McpGatewayAuthenticationOptions options,
        ILogger logger,
        string endpointName)
    {
        if (!options.LogRequestDiagnostics)
        {
            return;
        }

        var authorizationHeader = HeaderValue(request, "Authorization");
        var gatewayApiKey = HeaderValue(request, "x-afh-ai-gateway-key");
        var contentType = HeaderValue(request, "Content-Type");
        var userAgent = HeaderValue(request, "User-Agent");
        var correlationId = HeaderValue(request, "x-correlation-id");
        var contentLength = HeaderValue(request, "Content-Length");
        var authorizationScheme = ResolveAuthorizationScheme(authorizationHeader);
        var redactedCurl = CreateRedactedCurl(
            request,
            authorizationHeader,
            gatewayApiKey,
            contentType,
            correlationId);
        var sensitiveAuthorizationHeader = options.LogSensitiveAuthorizationHeader
            ? authorizationHeader
            : "<disabled>";
        var sensitiveCurl = options.LogSensitiveAuthorizationHeader
            ? CreateSensitiveCurl(
                request,
                authorizationHeader,
                gatewayApiKey,
                contentType,
                correlationId)
            : "<disabled>";

        logger.LogInformation(
            "MCP request diagnostics: Endpoint={Endpoint}; Method={Method}; Url={Url}; AuthorizationHeaderPresent={AuthorizationHeaderPresent}; AuthorizationScheme={AuthorizationScheme}; GatewayApiKeyPresent={GatewayApiKeyPresent}; ContentType={ContentType}; ContentLength={ContentLength}; UserAgent={UserAgent}; CorrelationId={CorrelationId}; RedactedCurl={RedactedCurl}; SensitiveAuthorizationHeader={SensitiveAuthorizationHeader}; SensitiveCurl={SensitiveCurl}",
            endpointName,
            request.Method,
            request.Url,
            !string.IsNullOrWhiteSpace(authorizationHeader),
            authorizationScheme,
            !string.IsNullOrWhiteSpace(gatewayApiKey),
            contentType,
            contentLength,
            userAgent,
            correlationId,
            redactedCurl,
            sensitiveAuthorizationHeader,
            sensitiveCurl);
    }

    private static string CreateRedactedCurl(
        HttpRequestData request,
        string? authorizationHeader,
        string? gatewayApiKey,
        string? contentType,
        string? correlationId)
    {
        var parts = new List<string>
        {
            "curl",
            "-X",
            request.Method,
            Quote(request.Url.ToString())
        };

        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            parts.Add("-H");
            parts.Add(Quote($"Authorization: {ResolveAuthorizationScheme(authorizationHeader)} <redacted>"));
        }

        if (!string.IsNullOrWhiteSpace(gatewayApiKey))
        {
            parts.Add("-H");
            parts.Add(Quote("x-afh-ai-gateway-key: <redacted>"));
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            parts.Add("-H");
            parts.Add(Quote($"Content-Type: {contentType}"));
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            parts.Add("-H");
            parts.Add(Quote($"x-correlation-id: {correlationId}"));
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("-d");
            parts.Add(Quote("<body omitted>"));
        }

        return string.Join(' ', parts);
    }

    private static string CreateSensitiveCurl(
        HttpRequestData request,
        string? authorizationHeader,
        string? gatewayApiKey,
        string? contentType,
        string? correlationId)
    {
        var parts = new List<string>
        {
            "curl",
            "-X",
            request.Method,
            Quote(request.Url.ToString())
        };

        if (!string.IsNullOrWhiteSpace(authorizationHeader))
        {
            parts.Add("-H");
            parts.Add(Quote($"Authorization: {authorizationHeader}"));
        }

        if (!string.IsNullOrWhiteSpace(gatewayApiKey))
        {
            parts.Add("-H");
            parts.Add(Quote("x-afh-ai-gateway-key: <redacted>"));
        }

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            parts.Add("-H");
            parts.Add(Quote($"Content-Type: {contentType}"));
        }

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            parts.Add("-H");
            parts.Add(Quote($"x-correlation-id: {correlationId}"));
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("-d");
            parts.Add(Quote("<body omitted>"));
        }

        return string.Join(' ', parts);
    }

    private static string ResolveAuthorizationScheme(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return string.Empty;
        }

        var separatorIndex = authorizationHeader.IndexOf(' ', StringComparison.Ordinal);
        return separatorIndex <= 0
            ? "<unknown>"
            : authorizationHeader[..separatorIndex];
    }

    private static string? HeaderValue(HttpRequestData request, string name)
    {
        return request.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static string Quote(string value)
    {
        return "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
    }
}
