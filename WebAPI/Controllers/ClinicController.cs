using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

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
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.VeterinarianName))
        {
            return ValidationProblem("Clinic and veterinarian names are required.");
        }

        var clinic = await dbContext.Clinics.SingleOrDefaultAsync(candidate => candidate.Id == ClinicId);
        if (clinic is null)
        {
            return NotFound();
        }

        clinic.Name = request.Name.Trim();
        clinic.Address = request.Address.Trim();
        clinic.LogoUrl = request.LogoUrl.Trim();
        clinic.VeterinarianName = request.VeterinarianName.Trim();
        clinic.VeterinarianTitles = request.VeterinarianTitles.Trim();
        clinic.VeterinarianLicenseNumber = request.VeterinarianLicenseNumber.Trim();
        await dbContext.SaveChangesAsync();
        return Ok(MapClinicProfile(clinic));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardSummary>> GetDashboard()
    {
        var clinic = await dbContext.Clinics.SingleOrDefaultAsync(clinic => clinic.Id == ClinicId);
        if (clinic is null)
        {
            return NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tomorrow = today.AddDays(1);
        var clinicAppointments = await dbContext.Appointments
            .Where(appointment => appointment.ClinicId == ClinicId)
            .Include(appointment => appointment.Patient)
            .ToListAsync();
        var todayAppointments = clinicAppointments
            .Where(appointment => DateOnly.FromDateTime(appointment.StartsAt.DateTime) == today)
            .OrderBy(appointment => appointment.StartsAt)
            .ToList();
        var upcomingAppointments = clinicAppointments
            .Where(appointment =>
                DateOnly.FromDateTime(appointment.StartsAt.DateTime) >= today &&
                DateOnly.FromDateTime(appointment.StartsAt.DateTime) <= tomorrow)
            .OrderBy(appointment => appointment.StartsAt)
            .ToList();

        return Ok(new DashboardSummary(
            clinic.Name,
            todayAppointments.Count,
            await dbContext.Patients.CountAsync(patient => patient.ClinicId == ClinicId),
            0,
            upcomingAppointments.Select(appointment =>
            {
                var scheduledDate = DateOnly.FromDateTime(appointment.StartsAt.DateTime);
                var dayLabel = scheduledDate == today ? "Today" : "Tomorrow";
                return new AppointmentSummary(
                    appointment.Id,
                    appointment.StartsAt,
                    $"{dayLabel}, {appointment.StartsAt:HH:mm}",
                    appointment.Patient.Name,
                    appointment.Reason,
                    appointment.ClinicianName);
            }).ToList()));
    }

    private static ClinicProfile MapClinicProfile(Clinic clinic) => new(
        clinic.Name,
        clinic.Address,
        clinic.LogoUrl,
        clinic.VeterinarianName,
        clinic.VeterinarianTitles,
        clinic.VeterinarianLicenseNumber);
}