using System.Text.Json;

namespace AFH.AI.McpGateway.Function.Functions.Docs;

/// <summary>
/// Builds the MCP Gateway OpenAPI document consumed by Scalar.
/// </summary>
public static class McpGatewayOpenApiDocumentFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates the OpenAPI document JSON.
    /// </summary>
    /// <param name="requestUrl">The OpenAPI request URL.</param>
    /// <returns>The OpenAPI JSON document.</returns>
    public static string CreateOpenApiJson(Uri requestUrl)
    {
        var baseUrl = $"{requestUrl.Scheme}://{requestUrl.Authority}";

        var document = new
        {
            openapi = "3.1.0",
            info = new
            {
                title = "AFH AI MCP Gateway",
                version = "2026.7.0",
                description = "Agent-facing MCP Gateway for AFH AI tools. The primary endpoint is the JSON-RPC MCP endpoint at /api/mcp."
            },
            servers = new[]
            {
                new { url = baseUrl }
            },
            tags = new[]
            {
                new { name = "MCP", description = "MCP JSON-RPC transport." },
                new { name = "Tools", description = "Diagnostic REST endpoints for tool discovery and invocation." },
                new { name = "System", description = "Operational endpoints." }
            },
            paths = new Dictionary<string, object>
            {
                ["/api/health"] = new
                {
                    get = new
                    {
                        tags = new[] { "System" },
                        summary = "Health check",
                        operationId = "getHealth",
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new
                            {
                                description = "The gateway is healthy.",
                                content = JsonContent("HealthResponse")
                            }
                        }
                    }
                },
                ["/api/mcp"] = new
                {
                    post = new
                    {
                        tags = new[] { "MCP" },
                        summary = "Handle MCP JSON-RPC request",
                        description = "Supports initialize, notifications/initialized, tools/list, and tools/call.",
                        operationId = "handleMcp",
                        security = new object[]
                        {
                            new Dictionary<string, string[]> { ["bearerAuth"] = [] },
                            new Dictionary<string, string[]> { ["gatewayApiKey"] = [] }
                        },
                        requestBody = new
                        {
                            required = true,
                            content = JsonContent("McpJsonRpcRequest")
                        },
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new
                            {
                                description = "JSON-RPC response.",
                                content = JsonContent("McpJsonRpcResponse")
                            },
                            ["400"] = new
                            {
                                description = "Invalid JSON-RPC request.",
                                content = JsonContent("McpJsonRpcResponse")
                            },
                            ["401"] = new
                            {
                                description = "The caller is not authenticated.",
                                content = JsonContent("McpJsonRpcResponse")
                            }
                        }
                    }
                },
                ["/api/mcp/v1/tools"] = new
                {
                    get = new
                    {
                        tags = new[] { "Tools" },
                        summary = "List registered AI tools",
                        operationId = "listTools",
                        security = SecuritySchemes(),
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new
                            {
                                description = "Registered tool list.",
                                content = JsonContent("ToolListResponse")
                            },
                            ["401"] = new
                            {
                                description = "The caller is not authenticated.",
                                content = JsonContent("McpErrorResponse")
                            }
                        }
                    }
                },
                ["/api/mcp/v1/tools/{toolName}/invoke"] = new
                {
                    post = new
                    {
                        tags = new[] { "Tools" },
                        summary = "Invoke an AI tool through the diagnostic REST endpoint",
                        operationId = "invokeTool",
                        security = SecuritySchemes(),
                        parameters = new[]
                        {
                            new
                            {
                                name = "toolName",
                                @in = "path",
                                required = true,
                                schema = new { type = "string" },
                                description = "Registered tool name, for example booking.get_lifecycle."
                            }
                        },
                        requestBody = new
                        {
                            required = true,
                            content = JsonContent("McpToolInvocationRequest")
                        },
                        responses = new Dictionary<string, object>
                        {
                            ["200"] = new
                            {
                                description = "Tool invocation response.",
                                content = JsonContent("McpToolInvocationResponse")
                            },
                            ["400"] = new
                            {
                                description = "Invalid invocation request.",
                                content = JsonContent("McpErrorResponse")
                            },
                            ["401"] = new
                            {
                                description = "The caller is not authenticated.",
                                content = JsonContent("McpErrorResponse")
                            },
                            ["403"] = new
                            {
                                description = "The caller does not have permission to use the tool.",
                                content = JsonContent("McpErrorResponse")
                            },
                            ["404"] = new
                            {
                                description = "The requested tool is not registered.",
                                content = JsonContent("McpErrorResponse")
                            }
                        }
                    }
                }
            },
            components = new
            {
                securitySchemes = new
                {
                    bearerAuth = new
                    {
                        type = "http",
                        scheme = "bearer",
                        bearerFormat = "JWT",
                        description = "Microsoft Entra access token for the MCP Gateway API."
                    },
                    gatewayApiKey = new
                    {
                        type = "apiKey",
                        @in = "header",
                        name = "x-afh-ai-gateway-key",
                        description = "Local or legacy shared key used only when bearer authentication is disabled."
                    }
                },
                schemas = Schemas()
            }
        };

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static object SecuritySchemes()
    {
        return new object[]
        {
            new Dictionary<string, string[]> { ["bearerAuth"] = [] },
            new Dictionary<string, string[]> { ["gatewayApiKey"] = [] }
        };
    }

    private static object JsonContent(string schemaName)
    {
        return new Dictionary<string, object>
        {
            ["application/json"] = new
            {
                schema = Ref(schemaName)
            }
        };
    }

    private static object Ref(string schemaName)
    {
        return new Dictionary<string, string>
        {
            ["$ref"] = $"#/components/schemas/{schemaName}"
        };
    }

    private static object Schemas()
    {
        return new Dictionary<string, object>
        {
            ["HealthResponse"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["status"] = new { type = "string", example = "Healthy" },
                    ["service"] = new { type = "string", example = "AFH AI MCP Gateway" }
                },
                required = new[] { "status", "service" }
            },
            ["McpJsonRpcRequest"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["jsonrpc"] = new { type = "string", example = "2.0" },
                    ["id"] = new
                    {
                        oneOf = new object[]
                        {
                            new { type = "string" },
                            new { type = "integer" },
                            new { type = "null" }
                        },
                        example = "1"
                    },
                    ["method"] = new
                    {
                        type = "string",
                        @enum = new[] { "initialize", "notifications/initialized", "tools/list", "tools/call" }
                    },
                    ["params"] = new
                    {
                        type = "object",
                        additionalProperties = true,
                        description = "Method-specific MCP parameters."
                    }
                },
                required = new[] { "jsonrpc", "method" }
            },
            ["McpJsonRpcResponse"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["jsonrpc"] = new { type = "string", example = "2.0" },
                    ["id"] = new
                    {
                        oneOf = new object[]
                        {
                            new { type = "string" },
                            new { type = "integer" },
                            new { type = "null" }
                        }
                    },
                    ["result"] = new
                    {
                        type = "object",
                        additionalProperties = true
                    },
                    ["error"] = Ref("McpJsonRpcError")
                },
                required = new[] { "jsonrpc" }
            },
            ["McpJsonRpcError"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["code"] = new { type = "integer" },
                    ["message"] = new { type = "string" },
                    ["data"] = new
                    {
                        type = "object",
                        additionalProperties = true
                    }
                },
                required = new[] { "code", "message" }
            },
            ["ToolListResponse"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["version"] = new { type = "string", example = "2026-07-phase-1" },
                    ["tools"] = new
                    {
                        type = "array",
                        items = Ref("AiToolDescriptor")
                    }
                },
                required = new[] { "version", "tools" }
            },
            ["AiToolDescriptor"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["name"] = new { type = "string", example = "booking.get_lifecycle" },
                    ["ownerService"] = new { type = "string", example = "Booking" },
                    ["description"] = new { type = "string" },
                    ["sideEffectLevel"] = new { type = "string", @enum = new[] { "ReadOnly", "Write", "HighRisk" } },
                    ["requiredPermissions"] = new { type = "array", items = new { type = "string" } },
                    ["endpoint"] = Ref("AiToolEndpointDescriptor"),
                    ["parameters"] = new { type = "array", items = Ref("AiToolParameterDescriptor") }
                },
                required = new[] { "name", "ownerService", "description", "sideEffectLevel", "requiredPermissions", "endpoint", "parameters" }
            },
            ["AiToolEndpointDescriptor"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["method"] = new { type = "string" },
                    ["routeTemplate"] = new { type = "string" },
                    ["serviceBaseUrlSetting"] = new { type = "string" }
                },
                required = new[] { "method", "routeTemplate", "serviceBaseUrlSetting" }
            },
            ["AiToolParameterDescriptor"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["name"] = new { type = "string" },
                    ["type"] = new { type = "string" },
                    ["required"] = new { type = "boolean" },
                    ["description"] = new { type = "string" }
                },
                required = new[] { "name", "type", "required", "description" }
            },
            ["McpToolInvocationRequest"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["arguments"] = new
                    {
                        type = "object",
                        additionalProperties = true,
                        example = new { bookingId = "booking-1" }
                    },
                    ["reason"] = new { type = "string", nullable = true }
                },
                required = new[] { "arguments" }
            },
            ["McpToolInvocationResponse"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["toolName"] = new { type = "string" },
                    ["correlationId"] = new { type = "string" },
                    ["statusCode"] = new { type = "integer" },
                    ["content"] = new
                    {
                        type = "object",
                        additionalProperties = true
                    }
                },
                required = new[] { "toolName", "correlationId", "statusCode", "content" }
            },
            ["McpErrorResponse"] = new
            {
                type = "object",
                properties = new Dictionary<string, object>
                {
                    ["code"] = new { type = "string" },
                    ["message"] = new { type = "string" },
                    ["correlationId"] = new { type = "string" }
                },
                required = new[] { "code", "message", "correlationId" }
            }
        };
    }
}
