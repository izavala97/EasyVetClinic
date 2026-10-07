using System.Data;
using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api/appointments")]
[Authorize]
[RequireClinicAccess]
public sealed class AppointmentsController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;
    private static readonly string[] Statuses = ["Scheduled", "CheckedIn", "InProgress", "Completed", "Cancelled", "NoShow"];

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ScheduleAppointment>>> GetAppointments(
        [FromQuery] DateOnly? date, [FromQuery] int? year, [FromQuery] int? month)
    {
        if (year is < 1 or > 9998 || month is < 1 or > 12 || (year is null) != (month is null))
            return ValidationProblem("Supply a valid year and month together.");
        var clinic = await dbContext.Clinics.AsNoTracking().SingleAsync(candidate => candidate.Id == ClinicId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(clinic.TimeZoneId);
        var selectedDate = date ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        var first = year.HasValue ? new DateOnly(year.Value, month!.Value, 1) : selectedDate;
        var last = year.HasValue ? first.AddMonths(1) : first.AddDays(1);
        var records = await dbContext.Appointments.AsNoTracking().Include(appointment => appointment.Patient)
            .Where(appointment => appointment.ClinicId == ClinicId).ToListAsync();
        return Ok(records.Where(appointment =>
        {
            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(appointment.StartsAt, zone).DateTime);
            return localDate >= first && localDate < last;
        }).OrderBy(appointment => appointment.StartsAt).Select(MapAppointment).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ScheduleAppointment>> CreateAppointment(CreateAppointmentRequest request)
    {
        if (!ValidFields(request.Reason, request.DurationMinutes) || string.IsNullOrWhiteSpace(request.ClinicianObjectId))
            return ValidationProblem("Select a clinician, a reason, and a duration between 5 and 480 minutes.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await ClinicScheduling.LockAsync(dbContext, ClinicId);
        var startsAt = await ResolveStart(request.StartsAt, request.LocalStartsAt);
        if (startsAt is null) return ValidationProblem("The clinic-local date/time is invalid or ambiguous due to daylight saving.");
        var clinician = await GetClinician(request.ClinicianObjectId);
        if (clinician is null) return ValidationProblem("Select an active clinician in this clinic.");
        var patient = await dbContext.Patients.SingleOrDefaultAsync(candidate =>
            candidate.ClinicId == ClinicId && candidate.Id == request.PatientId && candidate.IsActive);
        if (patient is null) return NotFound();
        if (await HasOverlap(null, request.ClinicianObjectId, clinician.DisplayName, startsAt.Value, request.DurationMinutes))
            return Conflict(new ProblemDetails { Title = "This clinician already has an overlapping appointment." });
        var appointment = new Appointment
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, PatientId = patient.Id,
            StartsAt = startsAt.Value, Reason = request.Reason.Trim(),
            ClinicianName = clinician.DisplayName, ClinicianObjectId = request.ClinicianObjectId,
            DurationMinutes = request.DurationMinutes
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
        appointment.Patient = patient;
        return Created($"api/appointments/{appointment.Id}", MapAppointment(appointment));
    }

    [HttpPut("{appointmentId}")]
    public async Task<ActionResult<ScheduleAppointment>> UpdateAppointment(string appointmentId, UpdateAppointmentRequest request)
    {
        if (!ValidFields(request.Reason, request.DurationMinutes) || !Statuses.Contains(request.Status) ||
            string.IsNullOrWhiteSpace(request.ClinicianObjectId))
            return ValidationProblem("Select a clinician, valid status, reason, and duration between 5 and 480 minutes.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await ClinicScheduling.LockAsync(dbContext, ClinicId);
        var startsAt = await ResolveStart(request.StartsAt, request.LocalStartsAt);
        if (startsAt is null) return ValidationProblem("The clinic-local date/time is invalid or ambiguous due to daylight saving.");
        var appointment = await dbContext.Appointments.Include(candidate => candidate.Patient)
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == appointmentId);
        if (appointment is null) return NotFound();
        if (request.ExpectedVersion != appointment.Version)
            return Conflict(new ProblemDetails { Title = "The appointment changed. Reload it before saving." });
        if (appointment.ConsultationId is not null)
            return Conflict(new ProblemDetails { Title = "Manage an appointment with started care through its consultation." });
        if (request.Status is "InProgress" or "Completed")
            return ValidationProblem("Start or complete clinical care through a consultation.");
        var clinician = await GetClinician(request.ClinicianObjectId);
        if (clinician is null) return ValidationProblem("Select an active clinician in this clinic.");
        var patient = await dbContext.Patients.SingleOrDefaultAsync(candidate =>
            candidate.ClinicId == ClinicId && candidate.Id == request.PatientId && candidate.IsActive);
        if (patient is null) return NotFound();
        if (request.Status is not ("Cancelled" or "NoShow") &&
            await HasOverlap(appointment.Id, request.ClinicianObjectId, clinician.DisplayName, startsAt.Value, request.DurationMinutes))
            return Conflict(new ProblemDetails { Title = "This clinician already has an overlapping appointment." });
        appointment.PatientId = patient.Id;
        appointment.Patient = patient;
        appointment.StartsAt = startsAt.Value;
        appointment.Reason = request.Reason.Trim();
        appointment.ClinicianName = clinician.DisplayName;
        appointment.ClinicianObjectId = request.ClinicianObjectId;
        appointment.DurationMinutes = request.DurationMinutes;
        appointment.Status = request.Status;
        appointment.Version++;
        try { await dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "The appointment changed. Reload it before saving." }); }
        await transaction.CommitAsync();
        return Ok(MapAppointment(appointment));
    }

    private async Task<DateTimeOffset?> ResolveStart(DateTimeOffset instant, string? local)
    {
        if (local is null) return instant.ToUniversalTime();
        if (!DateTime.TryParseExact(local, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var wallTime))
            return null;
        var clinic = await dbContext.Clinics.AsNoTracking().SingleAsync(candidate => candidate.Id == ClinicId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(clinic.TimeZoneId);
        if (zone.IsInvalidTime(wallTime) || zone.IsAmbiguousTime(wallTime)) return null;
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(wallTime, zone));
    }

    private async Task<ClinicianSummary?> GetClinician(string objectId)
    {
        var member = await dbContext.ClinicUsers.AsNoTracking().SingleOrDefaultAsync(user =>
            user.ClinicId == ClinicId && user.EntraObjectId == objectId && user.IsActive &&
            (user.Role == ClinicRoles.Veterinarian || user.Role == ClinicRoles.ClinicAdmin));
        if (member is not null) return new ClinicianSummary(member.EntraObjectId, member.DisplayName, member.ProfessionalTitles, member.LicenseNumber);
        if (currentClinic.Role == ClinicRoles.SuperAdmin && User.FindFirst("oid")?.Value == objectId)
            return new ClinicianSummary(objectId, User.FindFirst("name")?.Value ?? "Platform support", "", "");
        return null;
    }

    private async Task<bool> HasOverlap(string? excludedId, string clinicianId, string name, DateTimeOffset start, int duration)
    {
        var appointments = await dbContext.Appointments.AsNoTracking().Where(candidate =>
            candidate.ClinicId == ClinicId && candidate.Id != excludedId &&
            (candidate.ClinicianObjectId == clinicianId || (candidate.ClinicianObjectId == "" && candidate.ClinicianName == name)) &&
            candidate.Status != "Cancelled" && candidate.Status != "NoShow").ToListAsync();
        var end = start.AddMinutes(duration);
        return appointments.Any(candidate => candidate.StartsAt < end && candidate.StartsAt.AddMinutes(candidate.DurationMinutes) > start);
    }

    private static bool ValidFields(string reason, int duration) =>
        !string.IsNullOrWhiteSpace(reason) && reason.Length <= 500 && duration is >= 5 and <= 480;

    private static ScheduleAppointment MapAppointment(Appointment appointment) => new(
        appointment.Id, appointment.PatientId, appointment.Patient.Name, appointment.StartsAt, appointment.Reason,
        appointment.ClinicianName, appointment.DurationMinutes, appointment.Status, appointment.ConsultationId,
        appointment.Version, appointment.ClinicianObjectId);
}
