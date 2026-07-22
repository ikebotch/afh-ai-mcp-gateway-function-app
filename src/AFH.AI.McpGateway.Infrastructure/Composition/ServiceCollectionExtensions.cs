using AFH.AI.McpGateway.Application.Abstractions;
using AFH.AI.McpGateway.Infrastructure.Audit;
using AFH.AI.McpGateway.Infrastructure.Http;
using AFH.AI.McpGateway.Infrastructure.Options;
using AFH.AI.McpGateway.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AFH.AI.McpGateway.Infrastructure.Composition;

/// <summary>
/// Registers MCP Gateway infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds infrastructure services for the MCP Gateway.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddMcpGatewayInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<McpGatewayOptions>()
            .Bind(configuration.GetSection(McpGatewayOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient("afh-downstream-tools");
        services.AddSingleton<McpGatewayAuthenticator>();
        services.AddScoped<IToolDownstreamClient, HttpToolDownstreamClient>();
        services.AddSingleton<IAiAuditSink>(provider =>
        {
            var gatewayOptions = provider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<McpGatewayOptions>>()
                .Value;
            var auditProvider = string.IsNullOrWhiteSpace(gatewayOptions.Audit.Provider)
                ? "Logging"
                : gatewayOptions.Audit.Provider;

            return auditProvider.Equals("TableStorage", StringComparison.OrdinalIgnoreCase)
                ? ActivatorUtilities.CreateInstance<TableStorageAiAuditSink>(provider)
                : ActivatorUtilities.CreateInstance<LoggingAiAuditSink>(provider);
        });

        return services;
    }
}
