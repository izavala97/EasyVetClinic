using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EasyVetClinic.Api.Authentication;

public sealed class DevelopmentAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Development";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var objectId = configuration["DevelopmentIdentity:ObjectId"];
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Task.FromResult(AuthenticateResult.Fail("DevelopmentIdentity:ObjectId must be configured."));
        }

        var displayName = configuration["DevelopmentIdentity:DisplayName"] ?? "Local developer";
        var identity = new ClaimsIdentity([
            new Claim("oid", objectId),
            new Claim(ClaimTypes.Name, displayName)
        ], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}