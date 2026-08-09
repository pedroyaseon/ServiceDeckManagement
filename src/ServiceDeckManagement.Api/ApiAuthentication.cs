using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ServiceDeckManagement.Api;

public static class ApiAuthenticationDefaults
{
    public const string Scheme = "ServiceDeckBearer";
}

public sealed class ApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiDatabase database)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization["Bearer ".Length..].Trim();
        var principal = await database.FindSessionAsync(token, Context.RequestAborted).ConfigureAwait(false);
        if (principal is null)
        {
            return AuthenticateResult.Fail("Sessão inválida.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, principal.Id),
            new Claim(ClaimTypes.Name, principal.Username),
            new Claim(ClaimTypes.Role, principal.Role),
            new Claim("session_id", principal.SessionId)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}

public sealed class BootstrapCode
{
    private readonly object sync = new();
    private byte[]? codeHash;
    private DateTimeOffset expiresAt;

    public string Generate()
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        lock (sync)
        {
            codeHash = SHA256.HashData(Encoding.ASCII.GetBytes(code));
            expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        }

        return code;
    }

    public bool Consume(string code)
    {
        if (code.Length != 16 || code.Any(character => !char.IsAsciiHexDigit(character)))
        {
            return false;
        }

        lock (sync)
        {
            if (codeHash is null || expiresAt <= DateTimeOffset.UtcNow)
            {
                return false;
            }

            var supplied = SHA256.HashData(Encoding.ASCII.GetBytes(code.ToUpperInvariant()));
            var valid = CryptographicOperations.FixedTimeEquals(codeHash, supplied);
            CryptographicOperations.ZeroMemory(supplied);
            if (valid)
            {
                CryptographicOperations.ZeroMemory(codeHash);
                codeHash = null;
            }

            return valid;
        }
    }
}
