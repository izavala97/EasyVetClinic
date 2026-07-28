using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Data;

public sealed class CurrentClinic(ClinicDbContext dbContext, IHttpContextAccessor httpContextAccessor)
{
    private ClinicUser? membership;

    public string Id => GetMembership().ClinicId;
    public string Role => GetMembership().Role;

    private ClinicUser GetMembership()
    {
        if (membership is not null)
        {
            return membership;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var objectId = httpContext?.User.FindFirstValue("oid");
        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new ClinicAccessException("An authenticated Entra object ID is required.");
        }

        var memberships = dbContext.ClinicUsers.AsNoTracking()
            .Where(user => user.EntraObjectId == objectId && user.IsActive)
            .ToList();
        var selectedClinicId = httpContext?.Request.Headers["X-EasyVet-Clinic-Id"].FirstOrDefault();
        membership = string.IsNullOrWhiteSpace(selectedClinicId)
            ? memberships.Count == 1 ? memberships[0] : null
            : memberships.SingleOrDefault(user => user.ClinicId == selectedClinicId);
        return membership ?? throw new ClinicAccessException("This user does not have access to the selected clinic.");
    }
}

public sealed class ClinicAccessException(string message) : Exception(message);