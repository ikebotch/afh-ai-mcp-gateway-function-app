using AFH.AI.McpGateway.Application.Abstractions;
using AFH.Common.AI.Audit;
using Microsoft.Extensions.Logging;

namespace AFH.AI.McpGateway.Infrastructure.Audit;

/// <summary>
/// Writes AI audit events to structured logs for Phase 1.
/// </summary>
public sealed class LoggingAiAuditSink(ILogger<LoggingAiAuditSink> logger) : IAiAuditSink
{
    /// <inheritdoc />
    public Task WriteAsync(AiAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "AI tool audit {EventId} {ToolName} {Outcome} actor={ActorId} agent={AgentId} correlation={CorrelationId} service={DownstreamService} method={DownstreamMethod} target={DownstreamTarget} mode={ExecutionMode} status={StatusCode} durationMs={DurationMs} failure={FailureReason} request={RequestPayload} response={ResponsePayload}",
            auditEvent.EventId,
            auditEvent.ToolName,
            auditEvent.Outcome,
            auditEvent.ActorId,
            auditEvent.AgentId,
            auditEvent.CorrelationId,
            auditEvent.DownstreamService,
            auditEvent.DownstreamMethod,
            auditEvent.DownstreamTarget,
            auditEvent.ExecutionMode,
            auditEvent.StatusCode,
            auditEvent.DurationMs,
            auditEvent.FailureReason,
            auditEvent.RequestPayload,
            auditEvent.ResponsePayload);

        return Task.CompletedTask;
    }
}
