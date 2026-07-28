using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EasyVetClinic.Api.Authentication;

public sealed class EasyAuthAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "EasyAuth";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var encodedPrincipal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(encodedPrincipal!));
            var principal = JsonSerializer.Deserialize<EasAuthPrincipal>(json);
            var claims = principal?.Claims?.Select(claim => new Claim(NormalizeClaimType(claim.Type), claim.Value)).ToList() ?? [];
            var objectId = claims.FirstOrDefault(claim => claim.Type == "oid")?.Value;
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Easy Auth did not provide an Entra object ID."));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal header is not valid Base64."));
        }
        catch (JsonException)
        {
            return Task.FromResult(AuthenticateResult.Fail("The Easy Auth principal header is not valid JSON."));
        }
    }

    private static string NormalizeClaimType(string type) => type switch
    {
        "http://schemas.microsoft.com/identity/claims/objectidentifier" => "oid",
        "oid" => "oid",
        _ => type
    };

    private sealed record EasAuthPrincipal(IReadOnlyList<EasyAuthClaim>? Claims);
    private sealed record EasyAuthClaim(string Type, string Value);
}