using AFH.AI.McpGateway.Application.Composition;
using AFH.AI.McpGateway.Infrastructure.Composition;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        AddFlattenedValuesSection(cfg);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddMcpGatewayApplication();
        services.AddMcpGatewayInfrastructure(ctx.Configuration);
        services.Configure<WorkerOptions>(options =>
        {
            options.Serializer = new JsonObjectSerializer(
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
        });
    })
    .Build();

host.Run();

static void AddFlattenedValuesSection(IConfigurationBuilder cfg)
{
    var values = cfg.Build()
        .GetSection("Values")
        .AsEnumerable()
        .Where(kv => kv.Value is not null)
        .ToDictionary<KeyValuePair<string, string?>, string, string?>(
            kv => kv.Key.Replace("Values:", "").Replace("__", ":"),
            kv => kv.Value);

    cfg.AddInMemoryCollection(values);
}
