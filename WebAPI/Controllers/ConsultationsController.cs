using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Data;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[RequireClinicAccess]
public sealed class ConsultationsController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

    [HttpGet("consultations")]
    public async Task<ActionResult<IReadOnlyList<ConsultationListItem>>> GetConsultations([FromQuery] string? query, [FromQuery] string? status)
    {
        var consultations = dbContext.Consultations
            .AsNoTracking()
            .Include(consultation => consultation.Patient)
            .ThenInclude(patient => patient.Guardian)
            .Where(consultation => consultation.ClinicId == ClinicId);

        if (!string.IsNullOrWhiteSpace(status) && status is "InProgress" or "Completed" or "Cancelled")
        {
            consultations = consultations.Where(consultation => consultation.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            consultations = consultations.Where(consultation =>
                consultation.Patient.Name.Contains(query) ||
                consultation.Patient.Guardian.Name.Contains(query) ||
                consultation.ClinicianName.Contains(query) ||
                consultation.Diagnosis.Contains(query));
        }

        var records = await consultations.ToListAsync();
        return Ok(records.OrderByDescending(consultation => consultation.StartedAt).Select(consultation => new ConsultationListItem(
            consultation.Id,
            consultation.PatientId,
            consultation.Patient.Name,
            consultation.Patient.Guardian.Name,
            consultation.ClinicianName,
            consultation.StartedAt,
            consultation.Status,
            consultation.Diagnosis)).ToList());
    }

    [HttpPost("patients/{patientId}/consultations")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.Veterinarian)]
    public async Task<ActionResult<ConsultationSummary>> StartConsultation(string patientId, StartConsultationRequest request)
    {
        var patient = await dbContext.Patients
            .SingleOrDefaultAsync(patient => patient.ClinicId == ClinicId && patient.Id == patientId);
        if (patient is null)
        {
            return NotFound();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Appointment? appointment = null;
        if (request.AppointmentId is not null)
        {
            await ClinicScheduling.LockAsync(dbContext, ClinicId);
            appointment = await dbContext.Appointments.SingleOrDefaultAsync(candidate =>
                candidate.ClinicId == ClinicId && candidate.Id == request.AppointmentId && candidate.PatientId == patientId);
            if (appointment is null) return NotFound();
            if (appointment.ConsultationId is not null)
            {
                var existing = await dbContext.Consultations.SingleAsync(candidate =>
                    candidate.ClinicId == ClinicId && candidate.Id == appointment.ConsultationId);
                return Ok(new ConsultationSummary(existing.Id, patient.Id, patient.Name, existing.ClinicianName, existing.StartedAt, existing.Status));
            }
            if (appointment.Status is "Cancelled" or "NoShow" or "Completed")
                return Conflict(new ProblemDetails { Title = "This appointment cannot start a consultation." });
        }

        var actorId = User.FindFirstValue("oid")!;
        var clinicianId = request.ClinicianObjectId ?? appointment?.ClinicianObjectId ?? actorId;
        if (appointment?.ClinicianObjectId is not null && appointment.ClinicianObjectId != clinicianId)
            return ValidationProblem("Reassign the appointment before starting care with a different clinician.");
        var clinician = await dbContext.ClinicUsers.AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.ClinicId == ClinicId && candidate.EntraObjectId == clinicianId && candidate.IsActive &&
            (candidate.Role == ClinicRoles.Veterinarian || candidate.Role == ClinicRoles.ClinicAdmin));
        if (clinician is null && !(currentClinic.Role == ClinicRoles.SuperAdmin && clinicianId == actorId))
            return ValidationProblem("Select an active clinician in this clinic.");
        var clinicianName = clinician?.DisplayName ?? User.FindFirstValue("name") ?? User.Identity?.Name ?? "Platform support";
        var consultation = new Consultation
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            PatientId = patient.Id,
            ClinicianName = clinicianName,
            ClinicianObjectId = clinicianId,
            ClinicianTitles = clinician?.ProfessionalTitles ?? "",
            ClinicianLicenseNumber = clinician?.LicenseNumber ?? "",
            StartedAt = DateTimeOffset.UtcNow,
            Status = "InProgress"
        };
        if (appointment is not null)
        {
            appointment.ConsultationId = consultation.Id;
            appointment.Status = "InProgress";
            appointment.Version++;
        }
        dbContext.Consultations.Add(consultation);
        try { await dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "The appointment changed. Reload it before starting care." }); }
        await transaction.CommitAsync();
        var summary = new ConsultationSummary(consultation.Id, patient.Id, patient.Name, consultation.ClinicianName, consultation.StartedAt, consultation.Status);
        return Created($"api/consultations/{consultation.Id}", summary);
    }

    [HttpGet("consultations/{consultationId}")]
    public async Task<ActionResult<ConsultationDetail>> GetConsultation(string consultationId)
    {
        var consultation = await dbContext.Consultations
            .AsNoTracking()
            .AsSplitQuery()
            .Include(candidate => candidate.Patient)
            .ThenInclude(patient => patient.Guardian)
            .Include(candidate => candidate.Prescription)
            .ThenInclude(prescription => prescription!.Items)
            .Include(candidate => candidate.Amendments)
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == consultationId);
        return consultation is null ? NotFound() : Ok(MapConsultation(consultation));
    }

    [HttpPut("consultations/{consultationId}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.Veterinarian)]
    public async Task<ActionResult<ConsultationDetail>> UpdateConsultation(string consultationId, UpdateConsultationRequest request)
    {
        if (request.Status is not ("InProgress" or "Completed"))
        {
            return ValidationProblem("Status must be InProgress or Completed.");
        }
        if (request.PrescriptionItems.Any(item => string.IsNullOrWhiteSpace(item.MedicationName) ||
            string.IsNullOrWhiteSpace(item.Presentation) || string.IsNullOrWhiteSpace(item.DosageDirections)))
            return ValidationProblem("Each prescription item requires medication, presentation, and dosage directions.");

        var consultation = await dbContext.Consultations
            .Include(candidate => candidate.Patient)
            .ThenInclude(patient => patient.Guardian)
            .Include(candidate => candidate.Prescription)
            .ThenInclude(prescription => prescription!.Items)
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == consultationId);
        if (consultation is null)
        {
            return NotFound();
        }
        if (consultation.Status != "InProgress")
            return Conflict(new ProblemDetails { Title = "Finalized records cannot be overwritten. Add an amendment instead." });
        if (request.ExpectedVersion != consultation.Version)
            return Conflict(new ProblemDetails { Title = "The consultation changed. Reload it before saving." });

        consultation.ChiefComplaint = request.ChiefComplaint.Trim();
        consultation.ClinicalNotes = request.ClinicalNotes.Trim();
        consultation.Diagnosis = request.Diagnosis.Trim();
        consultation.Instructions = request.Instructions.Trim();
        consultation.Status = request.Status;
        consultation.Version++;
        var prescription = consultation.Prescription;
        if (prescription is null)
        {
            prescription = new Prescription
            {
                Id = Guid.NewGuid().ToString(),
                ClinicId = ClinicId,
                ConsultationId = consultation.Id
            };
            consultation.Prescription = prescription;
        }

        var retainedIds = request.PrescriptionItems.Where(item => item.Id is not null).Select(item => item.Id).ToHashSet();
        if (retainedIds.Count != request.PrescriptionItems.Count(item => item.Id is not null) ||
            retainedIds.Any(id => !prescription.Items.Any(item => item.Id == id)))
            return ValidationProblem("Prescription item identifiers must belong to this consultation and cannot be repeated.");
        foreach (var removed in prescription.Items.Where(item => !retainedIds.Contains(item.Id)).ToList())
        {
            dbContext.PrescriptionItems.Remove(removed);
            prescription.Items.Remove(removed);
        }
        foreach (var item in request.PrescriptionItems.OrderBy(item => item.SortOrder))
        {
            var existingItem = prescription.Items.SingleOrDefault(candidate => candidate.Id == item.Id);
            if (existingItem is null)
            {
                existingItem = new PrescriptionItem { Id = Guid.NewGuid().ToString() };
                prescription.Items.Add(existingItem);
            }
            existingItem.MedicationName = item.MedicationName.Trim();
            existingItem.Presentation = item.Presentation.Trim();
            existingItem.Concentration = item.Concentration.Trim();
            existingItem.DosageDirections = item.DosageDirections.Trim();
            existingItem.SortOrder = item.SortOrder;
        }

        prescription.DiagnosisSnapshot = consultation.Diagnosis;
        prescription.Instructions = consultation.Instructions;
        var isCompleting = request.Status == "Completed" && !prescription.IsFinalized;
        prescription.IsFinalized = request.Status == "Completed";
        prescription.FinalizedAt = isCompleting ? DateTimeOffset.UtcNow : prescription.FinalizedAt;
        prescription.LastUpdatedAt = DateTimeOffset.UtcNow;
        if (request.Status == "Completed")
        {
            var visitDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(consultation.StartedAt,
                TimeZoneInfo.FindSystemTimeZoneById((await dbContext.Clinics.SingleAsync(clinic => clinic.Id == ClinicId)).TimeZoneId)).DateTime);
            if (consultation.Patient.LastVisit is null || consultation.Patient.LastVisit < visitDate)
                consultation.Patient.LastVisit = visitDate;
            var appointment = await dbContext.Appointments.SingleOrDefaultAsync(candidate =>
                candidate.ClinicId == ClinicId && candidate.ConsultationId == consultation.Id);
            if (appointment is not null) { appointment.Status = "Completed"; appointment.Version++; }
        }
        try { await dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "The consultation changed. Reload it before saving." }); }
        return Ok(MapConsultation(consultation));
    }

    [HttpDelete("consultations/{consultationId}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.SuperAdmin, ClinicRoles.Veterinarian, ClinicRoles.Staff)]
    public async Task<IActionResult> DeleteConsultation(string consultationId)
    {
        var consultation = await dbContext.Consultations
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == consultationId);

        if (consultation is null)
        {
            return NotFound();
        }

        return Conflict(new ProblemDetails { Title = "Clinical records are retained. Use cancellation or an amendment instead of deletion." });
    }

    [HttpPost("consultations/{consultationId}/cancel")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.Veterinarian)]
    public async Task<IActionResult> CancelConsultation(string consultationId, CancelConsultationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            return ValidationProblem("A cancellation reason of at most 1000 characters is required.");
        var record = await dbContext.Consultations.SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == consultationId);
        if (record is null) return NotFound();
        if (record.Status != "InProgress" || record.Version != request.ExpectedVersion)
            return Conflict(new ProblemDetails { Title = "Only the current in-progress record can be cancelled." });
        record.Status = "Cancelled";
        record.CancellationReason = request.Reason.Trim();
        record.Version++;
        var appointment = await dbContext.Appointments.SingleOrDefaultAsync(candidate =>
            candidate.ClinicId == ClinicId && candidate.ConsultationId == record.Id);
        if (appointment is not null) { appointment.Status = "Cancelled"; appointment.Version++; }
        try { await dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "The record changed. Reload before cancelling." }); }
        return NoContent();
    }

    [HttpPost("consultations/{consultationId}/amendments")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.Veterinarian)]
    public async Task<IActionResult> AddAmendment(string consultationId, CreateClinicalAmendmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.Note) ||
            request.Reason.Length > 1000 || request.Note.Length > 10000)
            return ValidationProblem("An amendment requires a reason (up to 1000 characters) and note (up to 10000 characters).");
        var record = await dbContext.Consultations.SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == consultationId);
        if (record is null) return NotFound();
        if (record.Status != "Completed" || record.Version != request.ExpectedVersion)
            return Conflict(new ProblemDetails { Title = "Only the current completed record can be amended." });
        record.Version++;
        dbContext.ClinicalAmendments.Add(new ClinicalAmendment
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, ConsultationId = record.Id,
            AuthorObjectId = User.FindFirstValue("oid")!, AuthorName = User.FindFirstValue("name") ?? User.Identity?.Name ?? "",
            Reason = request.Reason.Trim(), Note = request.Note.Trim(), CreatedAt = DateTimeOffset.UtcNow
        });
        try { await dbContext.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new ProblemDetails { Title = "The record changed. Reload before adding the amendment." }); }
        return NoContent();
    }

    private static ConsultationDetail MapConsultation(Consultation consultation) => new(
        consultation.Id,
        consultation.PatientId,
        consultation.Patient.Name,
        consultation.Patient.Guardian.Name,
        consultation.ClinicianName,
        consultation.StartedAt,
        consultation.Status,
        consultation.ChiefComplaint,
        consultation.ClinicalNotes,
        consultation.Diagnosis,
        consultation.Instructions,
        consultation.Prescription?.LastUpdatedAt,
        consultation.Prescription?.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => new PrescriptionItemSummary(item.Id, item.MedicationName, item.Presentation, item.Concentration, item.DosageDirections, item.SortOrder))
            .ToList() ?? [],
        consultation.Version,
        consultation.ClinicianTitles,
        consultation.ClinicianLicenseNumber,
        consultation.CancellationReason,
        consultation.Amendments.OrderBy(item => item.CreatedAt).Select(item =>
            new ClinicalAmendmentSummary(item.Id, item.AuthorName, item.Reason, item.Note, item.CreatedAt)).ToList());
}
