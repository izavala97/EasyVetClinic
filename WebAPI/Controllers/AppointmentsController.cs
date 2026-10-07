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
[RequireClinicAccess]
public sealed class AppointmentsController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

    [HttpGet("appointments")]
    public async Task<ActionResult<IReadOnlyList<ScheduleAppointment>>> GetAppointments([FromQuery] DateOnly? date, [FromQuery] int? year, [FromQuery] int? month)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = year is not null && month is not null
            ? new DateOnly(year.Value, month.Value, 1)
            : selectedDate;
        var endDate = year is not null && month is not null
            ? startDate.AddMonths(1)
            : startDate.AddDays(1);
        var startOfRange = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfRange = endDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var clinicAppointments = await dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Where(appointment => appointment.ClinicId == ClinicId)
            .ToListAsync();

        return Ok(clinicAppointments
            .Where(appointment => appointment.StartsAt >= startOfRange && appointment.StartsAt < endOfRange)
            .OrderBy(appointment => appointment.StartsAt)
            .Select(MapAppointment)
            .ToList());
    }

    [HttpPost("appointments")]
    public async Task<ActionResult<ScheduleAppointment>> CreateAppointment(CreateAppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ClinicianName))
        {
            return ValidationProblem("Reason and clinician name are required.");
        }

        var patient = await dbContext.Patients
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == request.PatientId);
        if (patient is null)
        {
            return NotFound();
        }

        if (await dbContext.Appointments.AnyAsync(appointment =>
                appointment.ClinicId == ClinicId && appointment.StartsAt == request.StartsAt))
        {
            return Conflict(new ProblemDetails
            {
                Title = "The selected appointment time is already booked.",
                Status = StatusCodes.Status409Conflict
            });
        }

        var appointment = new Appointment
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            PatientId = patient.Id,
            StartsAt = request.StartsAt,
            Reason = request.Reason.Trim(),
            ClinicianName = request.ClinicianName.Trim()
        };
        dbContext.Appointments.Add(appointment);
        await dbContext.SaveChangesAsync();

        return Created($"api/appointments/{appointment.Id}", MapAppointment(appointment, patient.Name));
    }

    [HttpPut("appointments/{appointmentId}")]
    public async Task<ActionResult<ScheduleAppointment>> UpdateAppointment(string appointmentId, UpdateAppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ClinicianName))
        {
            return ValidationProblem("Reason and clinician name are required.");
        }

        var appointment = await dbContext.Appointments
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == appointmentId);
        if (appointment is null)
        {
            return NotFound();
        }

        var patient = await dbContext.Patients
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == request.PatientId);
        if (patient is null)
        {
            return NotFound();
        }

        if (await dbContext.Appointments.AnyAsync(candidate =>
                candidate.ClinicId == ClinicId &&
                candidate.Id != appointmentId &&
                candidate.StartsAt == request.StartsAt))
        {
            return Conflict(new ProblemDetails
            {
                Title = "The selected appointment time is already booked.",
                Status = StatusCodes.Status409Conflict
            });
        }

        appointment.PatientId = patient.Id;
        appointment.StartsAt = request.StartsAt;
        appointment.Reason = request.Reason.Trim();
        appointment.ClinicianName = request.ClinicianName.Trim();
        await dbContext.SaveChangesAsync();

        return Ok(MapAppointment(appointment, patient.Name));
    }

    private static ScheduleAppointment MapAppointment(Appointment appointment) =>
        MapAppointment(appointment, appointment.Patient.Name);

    private static ScheduleAppointment MapAppointment(Appointment appointment, string patientName) => new(
        appointment.Id,
        appointment.PatientId,
        patientName,
        appointment.StartsAt,
        appointment.Reason,
        appointment.ClinicianName);
}
