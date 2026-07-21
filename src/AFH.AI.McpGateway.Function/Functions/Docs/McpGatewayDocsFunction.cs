using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.AI.McpGateway.Function.Functions.Docs;

/// <summary>
/// Exposes OpenAPI and Scalar documentation for the MCP Gateway.
/// </summary>
public sealed class McpGatewayDocsFunction
{
    private const string ScalarHtml = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>AFH AI MCP Gateway Docs</title>
</head>
<body>
  <script id="api-reference" data-url="/api/openapi/v1.json" data-configuration='{"theme":"default","layout":"modern"}'></script>
  <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
</body>
</html>
""";

    /// <summary>
    /// Returns the MCP Gateway OpenAPI document.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(GetOpenApi))]
    public async Task<HttpResponseData> GetOpenApi(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "openapi/v1.json")] HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(McpGatewayOpenApiDocumentFactory.CreateOpenApiJson(request.Url)).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Returns the Scalar API reference UI.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(GetScalar))]
    public async Task<HttpResponseData> GetScalar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "scalar")] HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await response.WriteStringAsync(ScalarHtml).ConfigureAwait(false);
        return response;
    }

    /// <summary>
    /// Returns a route-scoped Scalar API reference UI alias.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The HTTP response.</returns>
    [Function(nameof(GetMcpScalar))]
    public Task<HttpResponseData> GetMcpScalar(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mcp/scalar")] HttpRequestData request)
    {
        return GetScalar(request);
    }
}
