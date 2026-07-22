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
| `GET` | `/api/openapi/v1.json` | OpenAPI document used by Scalar. |
| `GET` | `/api/scalar` | Scalar API reference UI. |
| `GET` | `/api/mcp/scalar` | Route-scoped Scalar API reference UI alias. |
| `POST` | `/api/mcp` | JSON-RPC MCP endpoint for agent clients. |
| `GET` | `/api/mcp/v1/tools` | Lists the Phase 1 AI tools. |
| `POST` | `/api/mcp/v1/tools/{toolName}/invoke` | Invokes a registered AI tool. |

The Copilot-facing MCP endpoint is `/api/mcp`. The `/api/mcp/v1/*` endpoints are retained for local diagnostics and simple HTTP smoke tests.

## MCP Methods

The JSON-RPC MCP endpoint supports:

| Method | Purpose |
| --- | --- |
| `initialize` | Returns gateway protocol capabilities and server metadata. |
| `notifications/initialized` | Accepts the client initialized notification. |
| `tools/list` | Returns registered tools and JSON input schemas. |
| `tools/call` | Invokes a registered AFH tool through the gateway policy, audit, and downstream invocation path. |

## Phase 1 Tools

| Tool | Owner | Downstream route |
| --- | --- | --- |
| `booking.get_my_bookings` | Booking | `GET /v1/admin/bookings` |
| `booking.search` | Booking | `GET /v1/admin/bookings` |
| `booking.get_details` | Booking | `GET /v1/bookings/{bookingId}` |
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

Temporary token diagnostics can be enabled without logging the bearer token value:

```bash
McpGateway__Authentication__LogTokenDiagnostics=true
```

This writes safe decoded JWT claim details such as audience, issuer, scopes, roles, tenant ID, and client ID to Application Insights. Turn it off after debugging authentication.

Redacted request diagnostics, including a curl-style request shape with secrets removed, can also be enabled:

```bash
McpGateway__Authentication__LogRequestDiagnostics=true
```

If you need to temporarily capture the raw Authorization header to inspect the exact access token sent by an agent, enable:

```bash
McpGateway__Authentication__LogSensitiveAuthorizationHeader=true
```

Only use this setting briefly, restrict log access while it is enabled, and turn it off immediately after debugging.

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

The safe Booking read tools are real downstream tools by default, even while global dry-run remains enabled:

```bash
booking.get_lifecycle
booking.find_availability
booking.get_my_bookings
booking.search
booking.get_details
```

Other tools stay in dry-run mode unless explicitly listed under `McpGateway__RealDownstreamTools`.

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
