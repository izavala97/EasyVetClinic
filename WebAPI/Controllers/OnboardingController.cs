using System.Security.Claims;
using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class OnboardingController(ClinicDbContext dbContext, PlatformAccess platformAccess,
    ILogger<OnboardingController> logger) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserSummary>> GetCurrentUser()
    {
        var objectId = User.FindFirstValue("oid");
        if (string.IsNullOrWhiteSpace(objectId)) return Forbid();

        var isOwner = platformAccess.IsOwner(User);
        var memberships = await dbContext.ClinicUsers.AsNoTracking().Include(user => user.Clinic)
            .Where(user => user.EntraObjectId == objectId && user.IsActive).ToListAsync();
        var clinics = isOwner
            ? await dbContext.Clinics.AsNoTracking().OrderBy(clinic => clinic.Name)
                .Select(clinic => new ClinicMembershipSummary(clinic.Id, clinic.Name, ClinicRoles.SuperAdmin, clinic.IsSuspended)).ToListAsync()
            : memberships.Select(user => new ClinicMembershipSummary(user.ClinicId, user.Clinic.Name, user.Role, user.Clinic.IsSuspended)).ToList();
        var selectedId = Request.Headers["X-EasyVet-Clinic-Id"].FirstOrDefault();
        var selected = string.IsNullOrWhiteSpace(selectedId)
            ? !isOwner && clinics.Count == 1 ? clinics[0] : null
            : clinics.SingleOrDefault(clinic => clinic.ClinicId == selectedId);
        if (!string.IsNullOrWhiteSpace(selectedId) && selected is null) return Forbid();

        return Ok(new CurrentUserSummary(objectId, User.Identity?.Name ?? "Signed-in user",
            selected?.ClinicId, selected?.ClinicName, selected?.Role, isOwner, selected?.IsSuspended ?? false, clinics));
    }

    [HttpPost("onboarding/clinic")]
    [RequirePlatformOwner]
    public async Task<ActionResult<PlatformClinicSummary>> CreateClinic(CreateInitialClinicRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.VeterinarianName)
            || request.Name.Length > 200 || request.VeterinarianName.Length > 200
            || request.Address.Length > 500 || request.VeterinarianTitles.Length > 300 || request.VeterinarianLicenseNumber.Length > 100)
        {
            return ValidationProblem("Valid clinic and veterinarian details are required.");
        }

        var clinic = new Clinic
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name.Trim(),
            Address = request.Address.Trim(),
            LogoUrl = request.LogoUrl.Trim(),
            VeterinarianName = request.VeterinarianName.Trim(),
            VeterinarianTitles = request.VeterinarianTitles.Trim(),
            VeterinarianLicenseNumber = request.VeterinarianLicenseNumber.Trim()
        };
        dbContext.Clinics.Add(clinic);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Platform owner {ActorId} created clinic {ClinicId}", User.FindFirstValue("oid"), clinic.Id);
        return Created($"api/platform/clinics/{clinic.Id}", new PlatformClinicSummary(clinic.Id, clinic.Name, false, "", 0));
    }
}
