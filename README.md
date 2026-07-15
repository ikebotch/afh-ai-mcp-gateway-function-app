# AFH AI MCP Gateway

Phase 1 implementation of Option B: a central AI-facing MCP Gateway that routes tool calls to service-owned APIs.

## Projects

- `AFH.AI.McpGateway.Contract` - gateway-specific contracts, referencing common AI/MCP SDK contracts.
- `AFH.AI.McpGateway.Application` - static Phase 1 tool registry, permission checks, invocation orchestration, and audit coordination.
- `AFH.AI.McpGateway.Infrastructure` - HTTP downstream tool client, structured logging audit sink, and Azure Table Storage audit sink.
- `AFH.AI.McpGateway.Function` - Azure Functions isolated worker host.

## Phase 1 Endpoints

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/health` | Health check. |
| `GET` | `/api/mcp/v1/tools` | Lists the Phase 1 AI tools. |
| `POST` | `/api/mcp/v1/tools/{toolName}/invoke` | Invokes a registered AI tool. |

## Phase 1 Tools

| Tool | Owner | Downstream route |
| --- | --- | --- |
| `booking.get_lifecycle` | Booking | `GET /v1/bookings/{bookingId}/lifecycle` |
| `booking.find_availability` | Booking | `POST /v2/transactions/{transactionId}/availability` |
| `calendar.get_schedule` | Calendar | `GET /v1/calendar/users/{userId}/schedule` |
| `notification.get_request` | Notification | `GET /v1/notifications/requests/{id}` |
| `location.route_time` | Location | `POST /v1/location/route-time` |
| `client.get_summary` | Client Integration | `GET /internal/client/v1/clients/{clientId}/summary` |
| `devops.create_user_story` | DevOps Integration | `POST /internal/devops/v1/work-items/user-stories` |
| `devops.get_current_sprint` | DevOps Integration | `GET /internal/devops/v1/sprints/current` |

## Headers

- `Authorization` - Microsoft Entra bearer access token from the MCP client. When `McpGateway__Authentication__Enabled=true`, the gateway validates this token before listing or invoking tools.
- `x-afh-ai-gateway-key` - optional local/legacy gateway API key when bearer authentication is disabled.
- `x-afh-ai-actor-id` - local/legacy user/service principal represented by the AI call when bearer authentication is disabled.
- `x-afh-ai-agent-id` - local/legacy AI client identity, such as Codex, Copilot, ChatGPT, or Claude, when bearer authentication is disabled.
- `x-afh-ai-permissions` - local/legacy comma-separated permissions used by Phase 1 policy checks when bearer authentication is disabled.
- `x-correlation-id` - optional correlation ID propagated downstream.
- `x-idempotency-key` - optional retry-safety key propagated to write-capable downstream services.
- `x-afh-ai-approved` - optional approval flag propagated to governed downstream services.
- `x-afh-ai-approval-id` - optional approval decision identifier propagated to governed downstream services.

## Microsoft Entra Authentication

For Microsoft Copilot agents, configure Copilot to request an access token for the MCP Gateway API and send it as `Authorization: Bearer <token>`.

Required Function App settings:

```bash
McpGateway__Authentication__Enabled=true
McpGateway__Authentication__TenantId=<tenant-id>
McpGateway__Authentication__Audience=api://afh-ai-mcp-gateway
```

Default scope mappings include:

| Entra scope | Internal permissions |
| --- | --- |
| `mcp.tools.read` | `ai.read` |
| `mcp.tools.write` | `ai.write` |
| `mcp.tools.booking.read` | `booking.read`, `availability.read` |
| `mcp.tools.devops.read` | `devops.sprints.read` |
| `mcp.tools.devops.write` | `ai.write`, `devops.workitems.write` |

## Local Behaviour

`McpGateway__DryRunDownstreamCalls` defaults to `true` in `local.settings.template.json`. In dry-run mode the gateway returns the downstream target it would call without invoking the service.

`booking.get_lifecycle` is the first real downstream tool and is listed under `McpGateway__RealDownstreamTools`. It calls the Booking service route `GET /v1/bookings/{bookingId}/lifecycle` when `Services__Booking__BaseUrl` points at a running Booking Function app.

## Audit

The gateway writes one audit event per authorized tool invocation. Phase 1 supports two providers:

| Provider | Configuration | Use |
| --- | --- | --- |
| `Logging` | `McpGateway__Audit__Provider=Logging` | Lightweight local diagnostics. |
| `TableStorage` | `McpGateway__Audit__Provider=TableStorage` | Durable Phase 1 audit using Azure Table Storage or Azurite. |

For local durable audit with Azurite:

```bash
McpGateway__Audit__Provider=TableStorage
McpGateway__Audit__ConnectionString=UseDevelopmentStorage=true
McpGateway__Audit__TableName=AiToolAudit
```

Audit records include the tool name, actor, agent, correlation ID, owning service, downstream target, execution mode, status code, duration, and failure reason when known.

Automatic service-published manifest discovery is intentionally left for Phase 2. Phase 1 uses `StaticToolRegistry` so the security and audit boundary can be proven first.
