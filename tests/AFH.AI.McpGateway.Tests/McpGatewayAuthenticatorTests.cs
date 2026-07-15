using AFH.AI.McpGateway.Infrastructure.Options;
using AFH.AI.McpGateway.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFH.AI.McpGateway.Tests;

public sealed class McpGatewayAuthenticatorTests
{
    [Fact]
    public async Task AuthenticateAsync_WhenBearerAuthDisabledAndApiKeyMatches_ReturnsLegacyActor()
    {
        var authenticator = CreateAuthenticator(new McpGatewayOptions
        {
            ApiKey = "expected-key",
            Authentication = new McpGatewayAuthenticationOptions { Enabled = false }
        });
        var request = new McpGatewayAuthenticationRequest(
            "Bearer delegated-token",
            "expected-key",
            "corr-1",
            "user-1",
            "copilot-agent",
            "tenant-1",
            ["booking.read"],
            "idem-1",
            "approval-1",
            true);

        var result = await authenticator.AuthenticateAsync(request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Actor);
        Assert.Equal("user-1", result.Actor.ActorId);
        Assert.Equal("copilot-agent", result.Actor.AgentId);
        Assert.Equal("tenant-1", result.Actor.TenantId);
        Assert.Equal("corr-1", result.Actor.CorrelationId);
        Assert.Contains("booking.read", result.Actor.Permissions);
        Assert.Equal("Bearer delegated-token", result.Actor.DelegatedAuthorizationHeader);
        Assert.Equal("idem-1", result.Actor.IdempotencyKey);
        Assert.Equal("approval-1", result.Actor.ApprovalId);
        Assert.True(result.Actor.IsApproved);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenBearerAuthDisabledAndApiKeyDoesNotMatch_Fails()
    {
        var authenticator = CreateAuthenticator(new McpGatewayOptions
        {
            ApiKey = "expected-key",
            Authentication = new McpGatewayAuthenticationOptions { Enabled = false }
        });
        var request = new McpGatewayAuthenticationRequest(
            null,
            "wrong-key",
            "corr-1",
            null,
            null,
            null,
            [],
            null,
            null,
            false);

        var result = await authenticator.AuthenticateAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("unauthorized", result.ErrorCode);
        Assert.Null(result.Actor);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenBearerAuthEnabledAndAuthorizationHeaderMissing_Fails()
    {
        var authenticator = CreateAuthenticator(new McpGatewayOptions
        {
            Authentication = new McpGatewayAuthenticationOptions
            {
                Enabled = true,
                TenantId = "tenant-id",
                Audience = "api://afh-ai-mcp-gateway"
            }
        });
        var request = new McpGatewayAuthenticationRequest(
            null,
            null,
            "corr-1",
            null,
            null,
            null,
            [],
            null,
            null,
            false);

        var result = await authenticator.AuthenticateAsync(request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("unauthorized", result.ErrorCode);
        Assert.Equal("A bearer access token is required.", result.ErrorMessage);
    }

    private static McpGatewayAuthenticator CreateAuthenticator(McpGatewayOptions options)
    {
        return new McpGatewayAuthenticator(
            Options.Create(options),
            NullLogger<McpGatewayAuthenticator>.Instance);
    }
}
