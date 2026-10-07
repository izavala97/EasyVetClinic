using System.Security.Claims;
using System.Text.Json;

namespace EasyVetClinic.Api.Authentication;

public sealed class PlatformAccess(IConfiguration configuration)
{
    public bool IsOwner(ClaimsPrincipal user)
    {
        var ownerId = configuration["PlatformAdministration:OwnerObjectId"];
        return user.Identity?.IsAuthenticated == true
            && !string.IsNullOrWhiteSpace(ownerId)
            && string.Equals(user.FindFirstValue("oid"), ownerId, StringComparison.Ordinal);
    }

    public static bool HasEmail(ClaimsPrincipal user, string email) =>
        user.FindAll("email").Concat(user.FindAll("emails")).Any(claim =>
        {
            if (claim.Value.StartsWith('['))
            {
                return JsonSerializer.Deserialize<string[]>(claim.Value)?.Any(value =>
                    string.Equals(value, email, StringComparison.OrdinalIgnoreCase)) == true;
            }

            return string.Equals(claim.Value, email, StringComparison.OrdinalIgnoreCase);
        });
}
