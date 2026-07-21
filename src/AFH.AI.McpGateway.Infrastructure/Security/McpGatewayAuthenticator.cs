using System.Security.Claims;
using AFH.AI.McpGateway.Infrastructure.Options;
using AFH.Common.AI.Actor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AFH.AI.McpGateway.Infrastructure.Security;

/// <summary>
/// Authenticates MCP callers using Microsoft Entra access tokens, with a legacy API-key fallback for local use.
/// </summary>
public sealed class McpGatewayAuthenticator(
    IOptions<McpGatewayOptions> options,
    ILogger<McpGatewayAuthenticator> logger)
{
    private readonly JsonWebTokenHandler _tokenHandler = new();
    private ConfigurationManager<OpenIdConnectConfiguration>? _configurationManager;
    private string? _configurationAuthority;

    /// <summary>
    /// Authenticates a caller and returns an actor context suitable for tool policy checks.
    /// </summary>
    /// <param name="request">The authentication request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authentication result.</returns>
    public async Task<McpGatewayAuthenticationResult> AuthenticateAsync(
        McpGatewayAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        var gatewayOptions = options.Value;
        if (!gatewayOptions.Authentication.Enabled)
        {
            return AuthenticateLegacy(request, gatewayOptions);
        }

        var token = ExtractBearerToken(request.AuthorizationHeader);
        if (string.IsNullOrWhiteSpace(token))
        {
            LogMissingBearerDiagnostics(request, gatewayOptions.Authentication);
            return McpGatewayAuthenticationResult.Failure(
                "unauthorized",
                "A bearer access token is required.");
        }

        var auth = gatewayOptions.Authentication;
        LogTokenDiagnostics(token, request, auth);
        if (string.IsNullOrWhiteSpace(auth.Audience))
        {
            logger.LogError("MCP Gateway authentication is enabled, but no audience is configured.");
            return McpGatewayAuthenticationResult.Failure(
                "server_misconfigured",
                "The MCP Gateway authentication audience is not configured.");
        }

        var authority = ResolveAuthority(auth);
        if (string.IsNullOrWhiteSpace(authority))
        {
            logger.LogError("MCP Gateway authentication is enabled, but no authority or tenant ID is configured.");
            return McpGatewayAuthenticationResult.Failure(
                "server_misconfigured",
                "The MCP Gateway authentication authority is not configured.");
        }

        try
        {
            var configuration = await GetConfigurationManager(authority)
                .GetConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);

            var validationResult = await _tokenHandler.ValidateTokenAsync(
                    token,
                    new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidAudience = auth.Audience,
                        ValidateIssuer = true,
                        ValidIssuer = configuration.Issuer,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKeys = configuration.SigningKeys,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(Math.Max(0, auth.ClockSkewMinutes))
                    })
                .ConfigureAwait(false);

            if (!validationResult.IsValid || validationResult.ClaimsIdentity is null)
            {
                logger.LogWarning(
                    validationResult.Exception,
                    "MCP Gateway bearer token validation failed. ConfiguredAudience={ConfiguredAudience}; ConfiguredAuthority={ConfiguredAuthority}; CorrelationId={CorrelationId}",
                    auth.Audience,
                    authority,
                    request.CorrelationId);
                return McpGatewayAuthenticationResult.Failure(
                    "unauthorized",
                    "The bearer access token is invalid.");
            }

            return McpGatewayAuthenticationResult.Success(
                CreateActor(request, validationResult.ClaimsIdentity, auth));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "MCP Gateway bearer token validation failed. ConfiguredAudience={ConfiguredAudience}; ConfiguredAuthority={ConfiguredAuthority}; CorrelationId={CorrelationId}",
                auth.Audience,
                authority,
                request.CorrelationId);
            return McpGatewayAuthenticationResult.Failure(
                "unauthorized",
                "The bearer access token could not be validated.");
        }
    }

    private static McpGatewayAuthenticationResult AuthenticateLegacy(
        McpGatewayAuthenticationRequest request,
        McpGatewayOptions gatewayOptions)
    {
        var expected = gatewayOptions.ApiKey;
        if (!string.IsNullOrWhiteSpace(expected) &&
            !string.Equals(request.GatewayApiKey, expected, StringComparison.Ordinal))
        {
            return McpGatewayAuthenticationResult.Failure(
                "unauthorized",
                "The MCP Gateway API key is invalid.");
        }

        return McpGatewayAuthenticationResult.Success(
            new AiActorContext(
                request.LegacyActorId ?? "unknown-actor",
                request.LegacyAgentId ?? "unknown-agent",
                request.LegacyTenantId,
                request.CorrelationId,
                request.LegacyPermissions,
                request.AuthorizationHeader,
                request.IdempotencyKey,
                request.ApprovalId,
                request.IsApproved));
    }

    private AiActorContext CreateActor(
        McpGatewayAuthenticationRequest request,
        ClaimsIdentity identity,
        McpGatewayAuthenticationOptions auth)
    {
        var claims = identity.Claims.ToArray();
        var scopes = claims
            .Where(claim => string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase))
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var roles = claims
            .Where(claim => string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value);

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddClaimsAndMappedPermissions(permissions, scopes, auth.ScopePermissionMappings);
        AddClaimsAndMappedPermissions(permissions, roles, auth.RolePermissionMappings);

        return new AiActorContext(
            FirstClaim(claims, "oid", "sub", ClaimTypes.NameIdentifier) ?? "unknown-actor",
            FirstClaim(claims, "azp", "appid", "aud", "client_id") ?? "microsoft-copilot-agent",
            FirstClaim(claims, "tid", "tenant_id"),
            request.CorrelationId,
            permissions.ToArray(),
            request.AuthorizationHeader,
            request.IdempotencyKey,
            request.ApprovalId,
            request.IsApproved);
    }

    private ConfigurationManager<OpenIdConnectConfiguration> GetConfigurationManager(string authority)
    {
        if (_configurationManager is not null &&
            string.Equals(_configurationAuthority, authority, StringComparison.OrdinalIgnoreCase))
        {
            return _configurationManager;
        }

        _configurationAuthority = authority;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
        return _configurationManager;
    }

    private static string? ResolveAuthority(McpGatewayAuthenticationOptions auth)
    {
        if (!string.IsNullOrWhiteSpace(auth.Authority))
        {
            return auth.Authority;
        }

        return string.IsNullOrWhiteSpace(auth.TenantId)
            ? null
            : $"https://login.microsoftonline.com/{auth.TenantId}/v2.0";
    }

    private static string? ExtractBearerToken(string? authorizationHeader)
    {
        const string bearerPrefix = "Bearer ";
        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorizationHeader[bearerPrefix.Length..].Trim();
    }

    private void LogMissingBearerDiagnostics(
        McpGatewayAuthenticationRequest request,
        McpGatewayAuthenticationOptions auth)
    {
        if (!auth.LogTokenDiagnostics)
        {
            return;
        }

        logger.LogInformation(
            "MCP token diagnostics: bearer token missing. AuthorizationHeaderPresent={AuthorizationHeaderPresent}; GatewayApiKeyPresent={GatewayApiKeyPresent}; ConfiguredAudience={ConfiguredAudience}; ConfiguredAuthority={ConfiguredAuthority}; CorrelationId={CorrelationId}",
            !string.IsNullOrWhiteSpace(request.AuthorizationHeader),
            !string.IsNullOrWhiteSpace(request.GatewayApiKey),
            auth.Audience,
            ResolveAuthority(auth),
            request.CorrelationId);
    }

    private void LogTokenDiagnostics(
        string token,
        McpGatewayAuthenticationRequest request,
        McpGatewayAuthenticationOptions auth)
    {
        if (!auth.LogTokenDiagnostics)
        {
            return;
        }

        try
        {
            if (!_tokenHandler.CanReadToken(token))
            {
                logger.LogInformation(
                    "MCP token diagnostics: bearer token is not a readable JWT. TokenLength={TokenLength}; ConfiguredAudience={ConfiguredAudience}; ConfiguredAuthority={ConfiguredAuthority}; CorrelationId={CorrelationId}",
                    token.Length,
                    auth.Audience,
                    ResolveAuthority(auth),
                    request.CorrelationId);
                return;
            }

            var jwt = _tokenHandler.ReadJsonWebToken(token);
            var scopes = string.Join(
                ' ',
                jwt.Claims
                    .Where(claim => string.Equals(claim.Type, "scp", StringComparison.OrdinalIgnoreCase))
                    .Select(claim => claim.Value));
            var roles = string.Join(
                ',',
                jwt.Claims
                    .Where(claim => string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
                    .Select(claim => claim.Value));

            logger.LogInformation(
                "MCP token diagnostics: Audiences={Audiences}; Issuer={Issuer}; Scopes={Scopes}; Roles={Roles}; ClientId={ClientId}; TenantId={TenantId}; Subject={Subject}; ExpiresUtc={ExpiresUtc}; ConfiguredAudience={ConfiguredAudience}; ConfiguredAuthority={ConfiguredAuthority}; CorrelationId={CorrelationId}",
                string.Join(',', jwt.Audiences),
                jwt.Issuer,
                scopes,
                roles,
                FirstClaim(jwt.Claims, "azp", "appid", "client_id"),
                FirstClaim(jwt.Claims, "tid", "tenant_id"),
                FirstClaim(jwt.Claims, "oid", "sub", ClaimTypes.NameIdentifier),
                jwt.ValidTo,
                auth.Audience,
                ResolveAuthority(auth),
                request.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogInformation(
                ex,
                "MCP token diagnostics could not decode bearer token. TokenLength={TokenLength}; ConfiguredAudience={ConfiguredAudience}; ConfiguredAuthority={ConfiguredAuthority}; CorrelationId={CorrelationId}",
                token.Length,
                auth.Audience,
                ResolveAuthority(auth),
                request.CorrelationId);
        }
    }

    private static void AddClaimsAndMappedPermissions(
        ISet<string> permissions,
        IEnumerable<string> claimValues,
        IReadOnlyDictionary<string, string[]> mappings)
    {
        foreach (var claimValue in claimValues.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            permissions.Add(claimValue);
            if (!mappings.TryGetValue(claimValue, out var mapped))
            {
                continue;
            }

            foreach (var permission in mapped)
            {
                permissions.Add(permission);
            }
        }
    }

    private static string? FirstClaim(IEnumerable<Claim> claims, params string[] types)
    {
        foreach (var type in types)
        {
            var match = claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}
