using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using EasyVetClinic.Api.Authentication;

namespace EasyVetClinic.Api.Data;

public sealed class CurrentClinic(ClinicDbContext dbContext, IHttpContextAccessor httpContextAccessor,
    PlatformAccess platformAccess, ILogger<CurrentClinic> logger)
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

        var selectedClinicId = httpContext?.Request.Headers["X-EasyVet-Clinic-Id"].FirstOrDefault();
        if (httpContext is not null && platformAccess.IsOwner(httpContext.User))
        {
            var clinic = dbContext.Clinics.AsNoTracking().SingleOrDefault(clinic => clinic.Id == selectedClinicId);
            if (clinic is null)
            {
                throw new ClinicAccessException("Select an existing clinic for support.");
            }

            logger.LogInformation("Platform owner {ActorId} accessed support workspace {ClinicId} via {Method} {Path}",
                objectId, clinic.Id, httpContext.Request.Method, httpContext.Request.Path);
            return membership = new ClinicUser { ClinicId = clinic.Id, Role = ClinicRoles.SuperAdmin };
        }

        var memberships = dbContext.ClinicUsers.AsNoTracking()
            .Include(user => user.Clinic)
            .Where(user => user.EntraObjectId == objectId && user.IsActive)
            .ToList();
        membership = string.IsNullOrWhiteSpace(selectedClinicId)
            ? memberships.Count == 1 ? memberships[0] : null
            : memberships.SingleOrDefault(user => user.ClinicId == selectedClinicId);
        if (membership is null || membership.Clinic.IsSuspended)
        {
            membership = null;
            throw new ClinicAccessException("This user does not have access to the selected clinic.");
        }

        return membership;
    }
}

public sealed class ClinicAccessException(string message) : Exception(message);