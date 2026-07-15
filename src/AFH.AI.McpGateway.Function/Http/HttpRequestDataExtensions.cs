using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace AFH.AI.McpGateway.Function.Http;

/// <summary>
/// HTTP helper extensions for Azure Functions responses.
/// </summary>
public static class HttpRequestDataExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Writes a JSON response.
    /// </summary>
    /// <param name="request">The request used to create the response.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="payload">The response payload.</param>
    /// <typeparam name="T">The payload type.</typeparam>
    /// <returns>The HTTP response.</returns>
    public static async Task<HttpResponseData> WriteJsonAsync<T>(
        this HttpRequestData request,
        HttpStatusCode statusCode,
        T payload)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("content-type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonOptions)).ConfigureAwait(false);
        return response;
    }
}
