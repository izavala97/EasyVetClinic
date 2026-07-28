using System.Security.Claims;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class OnboardingController(ClinicDbContext dbContext) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserSummary>> GetCurrentUser()
    {
        var objectId = User.FindFirstValue("oid");
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Forbid();
        }

        var membership = await dbContext.ClinicUsers.AsNoTracking()
            .Include(user => user.Clinic)
            .FirstOrDefaultAsync(user => user.EntraObjectId == objectId && user.IsActive);
        var displayName = User.Identity?.Name ?? membership?.DisplayName ?? "Signed-in user";
        return Ok(new CurrentUserSummary(objectId, displayName, membership?.ClinicId, membership?.Clinic?.Name, membership?.Role));
    }

    [HttpPost("onboarding/clinic")]
    public async Task<ActionResult<CurrentUserSummary>> CreateInitialClinic(CreateInitialClinicRequest request)
    {
        var objectId = User.FindFirstValue("oid");
        if (string.IsNullOrWhiteSpace(objectId))
        {
            return Forbid();
        }

        if (new[] { request.Name, request.VeterinarianName }.Any(string.IsNullOrWhiteSpace))
        {
            return ValidationProblem("Clinic and veterinarian names are required.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        if (await dbContext.Clinics.AnyAsync())
        {
            return Conflict(new { error = "Initial clinic setup has already been completed." });
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
        var displayName = User.Identity?.Name ?? "Clinic administrator";
        dbContext.Clinics.Add(clinic);
        dbContext.ClinicUsers.Add(new ClinicUser
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = clinic.Id,
            EntraObjectId = objectId,
            DisplayName = displayName,
            Role = ClinicRoles.ClinicAdmin
        });
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return Created("api/clinic", new CurrentUserSummary(objectId, displayName, clinic.Id, clinic.Name, ClinicRoles.ClinicAdmin));
    }
}