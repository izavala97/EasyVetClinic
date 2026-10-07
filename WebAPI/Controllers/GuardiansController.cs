using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[RequireClinicAccess]
public sealed class GuardiansController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

    [HttpGet("guardians")]
    public async Task<ActionResult<IReadOnlyList<GuardianSummary>>> GetGuardians()
    {
        var guardians = await dbContext.Guardians
            .AsNoTracking()
            .Where(guardian => guardian.ClinicId == ClinicId)
            .OrderBy(guardian => guardian.Name)
            .ToListAsync();
        return Ok(guardians.Select(MapGuardian).ToList());
    }

    [HttpPost("guardians")]
    public async Task<ActionResult<GuardianSummary>> CreateGuardian(CreateGuardianRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return ValidationProblem("Guardian name and phone are required.");
        }

        var normalizedPhone = new string(request.Phone.Where(char.IsDigit).ToArray());
        if (normalizedPhone.Length == 0)
        {
            return ValidationProblem("Guardian phone must contain only numbers.");
        }

        var normalizedAlternatePhone = request.AlternatePhone is null ? string.Empty : new string(request.AlternatePhone.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(request.AlternatePhone) && normalizedAlternatePhone.Length == 0)
        {
            return ValidationProblem("Guardian alternate phone must contain only numbers.");
        }

        var guardian = new Guardian
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            Name = request.Name.Trim(),
            Phone = normalizedPhone,
            AlternatePhone = normalizedAlternatePhone,
            Address = request.Address?.Trim() ?? string.Empty,
            IdentityType = request.IdentityType?.Trim() ?? string.Empty,
            IdentityNumber = request.IdentityNumber?.Trim() ?? string.Empty,
            IdentityDocumentUrl = request.IdentityDocumentUrl?.Trim() ?? string.Empty
        };
        dbContext.Guardians.Add(guardian);
        await dbContext.SaveChangesAsync();
        return Created($"api/guardians/{guardian.Id}", MapGuardian(guardian));
    }

    [HttpGet("guardians/{guardianId}")]
    public async Task<ActionResult<GuardianSummary>> GetGuardian(string guardianId)
    {
        var guardian = await dbContext.Guardians.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == guardianId);
        return guardian is null ? NotFound() : Ok(MapGuardian(guardian));
    }

    [HttpPut("guardians/{guardianId}")]
    public async Task<ActionResult<GuardianSummary>> UpdateGuardian(string guardianId, UpdateGuardianRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Phone))
        {
            return ValidationProblem("Guardian name and phone are required.");
        }

        var normalizedPhone = new string(request.Phone.Where(char.IsDigit).ToArray());
        if (normalizedPhone.Length == 0)
        {
            return ValidationProblem("Guardian phone must contain only numbers.");
        }

        var normalizedAlternatePhone = request.AlternatePhone is null ? string.Empty : new string(request.AlternatePhone.Where(char.IsDigit).ToArray());
        if (!string.IsNullOrEmpty(request.AlternatePhone) && normalizedAlternatePhone.Length == 0)
        {
            return ValidationProblem("Guardian alternate phone must contain only numbers.");
        }

        var guardian = await dbContext.Guardians
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == guardianId);
        if (guardian is null)
        {
            return NotFound();
        }

        guardian.Name = request.Name.Trim();
        guardian.Phone = normalizedPhone;
        guardian.AlternatePhone = normalizedAlternatePhone;
        guardian.Address = request.Address.Trim();
        guardian.IdentityType = request.IdentityType.Trim();
        guardian.IdentityNumber = request.IdentityNumber.Trim();
        guardian.IdentityDocumentUrl = request.IdentityDocumentUrl.Trim();
        await dbContext.SaveChangesAsync();
        return Ok(MapGuardian(guardian));
    }

    private static GuardianSummary MapGuardian(Guardian guardian) => new(
        guardian.Id,
        guardian.Name,
        guardian.Phone,
        guardian.AlternatePhone,
        guardian.Address,
        guardian.IdentityType,
        guardian.IdentityNumber,
        guardian.IdentityDocumentUrl);
}
