using AFH.Common.AI.Audit;

namespace AFH.AI.McpGateway.Application.Abstractions;

/// <summary>
/// Persists or emits audit events for AI tool invocations.
/// </summary>
public interface IAiAuditSink
{
    /// <summary>
    /// Writes an audit event.
    /// </summary>
    /// <param name="auditEvent">The audit event to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    Task WriteAsync(AiAuditEvent auditEvent, CancellationToken cancellationToken);
}
