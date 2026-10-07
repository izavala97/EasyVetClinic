using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[RequireClinicAccess]
public sealed class ClinicController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

    [HttpGet("clinic")]
    public async Task<ActionResult<ClinicProfile>> GetClinicProfile()
    {
        var clinic = await dbContext.Clinics.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == ClinicId);
        return clinic is null ? NotFound() : Ok(MapClinicProfile(clinic));
    }

    [HttpPut("clinic")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.SuperAdmin)]
    public async Task<ActionResult<ClinicProfile>> UpdateClinicProfile(UpdateClinicProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 || request.Address.Length > 500)
        {
            return ValidationProblem("A clinic name of at most 200 characters and address of at most 500 characters are required.");
        }
        if (string.IsNullOrWhiteSpace(request.TimeZoneId) || request.TimeZoneId.Length > 100)
            return ValidationProblem("Select a valid time zone identifier.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (TimeZoneNotFoundException) { return ValidationProblem("Select a valid time zone identifier."); }
        catch (InvalidTimeZoneException) { return ValidationProblem("The selected time zone is invalid."); }

        var clinic = await dbContext.Clinics.SingleOrDefaultAsync(candidate => candidate.Id == ClinicId);
        if (clinic is null)
        {
            return NotFound();
        }

        clinic.Name = request.Name.Trim();
        clinic.Address = request.Address.Trim();
        clinic.LogoUrl = request.LogoUrl.Trim();
        clinic.TimeZoneId = request.TimeZoneId;
        await dbContext.SaveChangesAsync();
        return Ok(MapClinicProfile(clinic));
    }

    [HttpGet("clinicians")]
    public async Task<ActionResult<IReadOnlyList<ClinicianSummary>>> GetClinicians()
    {
        var clinicians = await dbContext.ClinicUsers.AsNoTracking().Where(user =>
            user.ClinicId == ClinicId && user.IsActive &&
            (user.Role == ClinicRoles.ClinicAdmin || user.Role == ClinicRoles.Veterinarian))
            .OrderBy(user => user.DisplayName).Select(user =>
                new ClinicianSummary(user.EntraObjectId, user.DisplayName, user.ProfessionalTitles, user.LicenseNumber)).ToListAsync();
        if (currentClinic.Role == ClinicRoles.SuperAdmin)
            clinicians.Add(new ClinicianSummary(User.FindFirstValue("oid")!, User.FindFirstValue("name") ?? "Platform support", "", "", false));
        return Ok(clinicians);
    }

    [HttpPut("clinicians/{objectId}/credentials")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<IActionResult> UpdateClinicianCredentials(string objectId, UpdateClinicianCredentialsRequest request)
    {
        if (request.ProfessionalTitles.Length > 300 || request.LicenseNumber.Length > 100)
            return ValidationProblem("Titles must be at most 300 characters and license number at most 100.");
        var clinician = await dbContext.ClinicUsers.SingleOrDefaultAsync(user => user.ClinicId == ClinicId &&
            user.EntraObjectId == objectId && user.IsActive && (user.Role == ClinicRoles.ClinicAdmin || user.Role == ClinicRoles.Veterinarian));
        if (clinician is null) return NotFound();
        clinician.ProfessionalTitles = request.ProfessionalTitles.Trim();
        clinician.LicenseNumber = request.LicenseNumber.Trim();
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSummary>> GetDashboard()
    {
        var clinic = await dbContext.Clinics.SingleOrDefaultAsync(clinic => clinic.Id == ClinicId);
        if (clinic is null)
        {
            return NotFound();
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(clinic.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        var tomorrow = today.AddDays(1);
        var clinicAppointments = await dbContext.Appointments
            .Where(appointment => appointment.ClinicId == ClinicId && appointment.Status != "Cancelled" && appointment.Status != "NoShow")
            .Include(appointment => appointment.Patient)
            .ToListAsync();
        var todayAppointments = clinicAppointments
            .Where(appointment => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.StartsAt, zone).DateTime) == today)
            .OrderBy(appointment => appointment.StartsAt)
            .ToList();
        var upcomingAppointments = clinicAppointments
            .Where(appointment =>
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.StartsAt, zone).DateTime) >= today &&
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.StartsAt, zone).DateTime) <= tomorrow)
            .OrderBy(appointment => appointment.StartsAt)
            .ToList();

        return Ok(new DashboardSummary(
            clinic.Name,
            todayAppointments.Count,
            await dbContext.Patients.CountAsync(patient => patient.ClinicId == ClinicId && patient.IsActive),
            0,
            upcomingAppointments.Select(appointment =>
            {
                var localStart = TimeZoneInfo.ConvertTime(appointment.StartsAt, zone);
                var scheduledDate = DateOnly.FromDateTime(localStart.DateTime);
                var dayLabel = scheduledDate == today ? "Today" : "Tomorrow";
                return new AppointmentSummary(
                    appointment.Id,
                    appointment.StartsAt,
                    $"{dayLabel}, {localStart:HH:mm}",
                    appointment.Patient.Name,
                    appointment.Reason,
                    appointment.ClinicianName);
            }).ToList()));
    }

    private static ClinicProfile MapClinicProfile(Clinic clinic) => new(
        clinic.Name,
        clinic.Address,
        clinic.LogoUrl,
        clinic.TimeZoneId);
}