using AFH.AI.McpGateway.Application.Abstractions;
using AFH.AI.McpGateway.Infrastructure.Options;
using AFH.Common.AI.Audit;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AFH.AI.McpGateway.Infrastructure.Audit;

/// <summary>
/// Persists AI audit events to Azure Table Storage.
/// </summary>
public sealed class TableStorageAiAuditSink : IAiAuditSink
{
    private readonly TableClient tableClient;
    private readonly ILogger<TableStorageAiAuditSink> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TableStorageAiAuditSink"/> class.
    /// </summary>
    /// <param name="options">The MCP Gateway options.</param>
    /// <param name="logger">The logger.</param>
    public TableStorageAiAuditSink(
        IOptions<McpGatewayOptions> options,
        ILogger<TableStorageAiAuditSink> logger)
        : this(CreateTableClient(options.Value.Audit), logger)
    {
    }

    internal TableStorageAiAuditSink(
        TableClient tableClient,
        ILogger<TableStorageAiAuditSink> logger)
    {
        this.tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task WriteAsync(AiAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        try
        {
            await tableClient.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
            await tableClient.AddEntityAsync(ToEntity(auditEvent), cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(
                ex,
                "Failed to persist AI audit event {EventId} for tool {ToolName}.",
                auditEvent.EventId,
                auditEvent.ToolName);

            throw;
        }
    }

    internal static TableEntity ToEntity(AiAuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var service = NormalizeKey(auditEvent.DownstreamService);
        var occurred = auditEvent.OccurredUtc.ToUniversalTime();
        var partitionKey = $"{occurred:yyyyMMdd}|{service}";
        var rowKey = $"{DateTimeOffset.MaxValue.UtcTicks - occurred.UtcTicks:D19}|{auditEvent.EventId}";

        var entity = new TableEntity(partitionKey, rowKey)
        {
            ["EventId"] = auditEvent.EventId,
            ["OccurredUtc"] = occurred,
            ["ActorId"] = auditEvent.ActorId,
            ["AgentId"] = auditEvent.AgentId,
            ["ToolName"] = auditEvent.ToolName,
            ["Outcome"] = auditEvent.Outcome,
            ["CorrelationId"] = auditEvent.CorrelationId,
            ["DownstreamService"] = auditEvent.DownstreamService,
            ["ExecutionMode"] = auditEvent.ExecutionMode,
            ["DurationMs"] = auditEvent.DurationMs
        };

        if (!string.IsNullOrWhiteSpace(auditEvent.DownstreamTarget))
        {
            entity["DownstreamTarget"] = auditEvent.DownstreamTarget;
        }

        if (!string.IsNullOrWhiteSpace(auditEvent.DownstreamMethod))
        {
            entity["DownstreamMethod"] = auditEvent.DownstreamMethod;
        }

        if (!string.IsNullOrWhiteSpace(auditEvent.RequestPayload))
        {
            entity["RequestPayload"] = auditEvent.RequestPayload;
        }

        if (!string.IsNullOrWhiteSpace(auditEvent.ResponsePayload))
        {
            entity["ResponsePayload"] = auditEvent.ResponsePayload;
        }

        if (auditEvent.StatusCode.HasValue)
        {
            entity["StatusCode"] = auditEvent.StatusCode.Value;
        }

        if (!string.IsNullOrWhiteSpace(auditEvent.FailureReason))
        {
            entity["FailureReason"] = auditEvent.FailureReason;
        }

        return entity;
    }

    private static TableClient CreateTableClient(McpGatewayAuditOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("McpGateway:Audit:ConnectionString is required when the audit provider is TableStorage.");
        }

        if (string.IsNullOrWhiteSpace(options.TableName))
        {
            throw new InvalidOperationException("McpGateway:Audit:TableName is required when the audit provider is TableStorage.");
        }

        return new TableClient(options.ConnectionString, options.TableName);
    }

    private static string NormalizeKey(string value)
    {
        var normalized = value
            .Replace("/", "-", StringComparison.Ordinal)
            .Replace("\\", "-", StringComparison.Ordinal)
            .Replace("#", "-", StringComparison.Ordinal)
            .Replace("?", "-", StringComparison.Ordinal)
            .Trim();

        return string.IsNullOrWhiteSpace(normalized) ? "Unknown" : normalized;
    }
}
