using AFH.AI.McpGateway.Application.Abstractions;
using AFH.AI.McpGateway.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.AI.McpGateway.Application.Composition;

/// <summary>
/// Registers MCP Gateway application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds application services for the MCP Gateway.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddMcpGatewayApplication(this IServiceCollection services)
    {
        services.AddSingleton<IToolRegistry, StaticToolRegistry>();
        services.AddScoped<ToolInvocationService>();
        return services;
    }
}
