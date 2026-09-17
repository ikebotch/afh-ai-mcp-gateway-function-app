using System.Security.Cryptography;
using AFH.AI.McpGateway.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AFH.AI.McpGateway.Infrastructure.Http;

/// <summary>
/// Applies configured authentication for Snowflake Cortex agent REST calls.
/// </summary>
public sealed class SnowflakeAgentAuthenticator(IOptions<McpGatewayOptions> options) : ISnowflakeAgentAuthenticator
{
    private const string BearerTokenMode = "BearerToken";
    private const string KeyPairJwtMode = "KeyPairJwt";
    private const string TokenTypeHeaderName = "X-Snowflake-Authorization-Token-Type";
    private const int MaximumJwtLifetimeMinutes = 60;

    /// <inheritdoc />
    public void Apply(HttpRequestMessage request)
    {
        var snowflakeOptions = options.Value.SnowflakeAgent;
        var mode = string.IsNullOrWhiteSpace(snowflakeOptions.AuthenticationMode)
            ? BearerTokenMode
            : snowflakeOptions.AuthenticationMode.Trim();

        if (mode.Equals(KeyPairJwtMode, StringComparison.OrdinalIgnoreCase))
        {
            var jwt = CreateKeyPairJwt(snowflakeOptions);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {jwt}");
            request.Headers.TryAddWithoutValidation(TokenTypeHeaderName, "KEYPAIR_JWT");
            return;
        }

        if (!string.IsNullOrWhiteSpace(snowflakeOptions.BearerToken))
        {
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {snowflakeOptions.BearerToken.Trim()}");
        }
    }

    private static string CreateKeyPairJwt(McpGatewaySnowflakeAgentOptions options)
    {
        var accountIdentifier = NormalizeRequiredClaimPart(options.AccountIdentifier, "McpGateway:SnowflakeAgent:AccountIdentifier");
        var user = NormalizeRequiredClaimPart(options.User, "McpGateway:SnowflakeAgent:User");
        var privateKey = Require(options.PrivateKey, "McpGateway:SnowflakeAgent:PrivateKey");

        using var rsa = RSA.Create();
        ImportPrivateKey(rsa, privateKey, options.PrivateKeyPassphrase);

        var publicKeyFingerprint = CreatePublicKeyFingerprint(rsa);
        var qualifiedUser = $"{accountIdentifier}.{user}";
        var now = DateTimeOffset.UtcNow;
        var lifetimeMinutes = Math.Clamp(options.JwtLifetimeMinutes, 1, MaximumJwtLifetimeMinutes);
        var signingKey = new RsaSecurityKey(rsa)
        {
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false
            }
        };
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = $"{qualifiedUser}.{publicKeyFingerprint}",
            Subject = new System.Security.Claims.ClaimsIdentity(
            [
                new(JwtRegisteredClaimNames.Sub, qualifiedUser)
            ]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(lifetimeMinutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };

        return new JsonWebTokenHandler().CreateToken(tokenDescriptor);
    }

    private static void ImportPrivateKey(RSA rsa, string privateKey, string? passphrase)
    {
        var normalizedPrivateKey = privateKey.Replace("\\n", "\n", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(passphrase))
        {
            rsa.ImportFromPem(normalizedPrivateKey);
            return;
        }

        rsa.ImportFromEncryptedPem(normalizedPrivateKey, passphrase);
    }

    private static string CreatePublicKeyFingerprint(RSA rsa)
    {
        var publicKey = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKey);
        return $"SHA256:{Convert.ToBase64String(hash)}";
    }

    private static string NormalizeRequiredClaimPart(string? value, string settingName)
    {
        var required = Require(value, settingName);
        return required.Replace(".", "-", StringComparison.Ordinal).ToUpperInvariant();
    }

    private static string Require(string? value, string settingName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"{settingName} is required when Snowflake agent authentication mode is KeyPairJwt.")
            : value.Trim();
}
