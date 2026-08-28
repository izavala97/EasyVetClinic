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

        if (!string.IsNullOrWhiteSpace(status) && status is "InProgress" or "Completed")
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

        var consultation = new Consultation
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            PatientId = patient.Id,
            ClinicianName = request.ClinicianName,
            StartedAt = DateTimeOffset.UtcNow,
            Status = "InProgress"
        };
        dbContext.Consultations.Add(consultation);
        await dbContext.SaveChangesAsync();
        var summary = new ConsultationSummary(consultation.Id, patient.Id, patient.Name, consultation.ClinicianName, consultation.StartedAt, consultation.Status);
        return Created($"api/consultations/{consultation.Id}", summary);
    }

    [HttpGet("consultations/{consultationId}")]
    public async Task<ActionResult<ConsultationDetail>> GetConsultation(string consultationId)
    {
        var consultation = await dbContext.Consultations
            .AsNoTracking()
            .Include(candidate => candidate.Patient)
            .ThenInclude(patient => patient.Guardian)
            .Include(candidate => candidate.Prescription)
            .ThenInclude(prescription => prescription!.Items)
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

        consultation.ChiefComplaint = request.ChiefComplaint.Trim();
        consultation.ClinicalNotes = request.ClinicalNotes.Trim();
        consultation.Diagnosis = request.Diagnosis.Trim();
        consultation.Instructions = request.Instructions.Trim();
        consultation.Status = request.Status;
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

        dbContext.PrescriptionItems.RemoveRange(prescription.Items);
        prescription.Items.Clear();
        foreach (var item in request.PrescriptionItems.OrderBy(item => item.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(item.MedicationName) || string.IsNullOrWhiteSpace(item.Presentation) || string.IsNullOrWhiteSpace(item.DosageDirections))
            {
                return ValidationProblem("Each prescription item requires medication, presentation, and dosage directions.");
            }

            prescription.Items.Add(new PrescriptionItem
            {
                Id = Guid.NewGuid().ToString(),
                MedicationName = item.MedicationName.Trim(),
                Presentation = item.Presentation.Trim(),
                Concentration = item.Concentration.Trim(),
                DosageDirections = item.DosageDirections.Trim(),
                SortOrder = item.SortOrder
            });
        }

        prescription.DiagnosisSnapshot = consultation.Diagnosis;
        prescription.Instructions = consultation.Instructions;
        var isCompleting = request.Status == "Completed" && !prescription.IsFinalized;
        prescription.IsFinalized = request.Status == "Completed";
        prescription.FinalizedAt = isCompleting ? DateTimeOffset.UtcNow : prescription.FinalizedAt;
        prescription.LastUpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
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

        var isCompleted = consultation.Status == "Completed";
        var isClinicOwner = HttpContext.User.IsInRole(ClinicRoles.ClinicAdmin) || HttpContext.User.IsInRole(ClinicRoles.SuperAdmin);
        if (isCompleted && !isClinicOwner)
        {
            return Forbid();
        }

        dbContext.Consultations.Remove(consultation);
        await dbContext.SaveChangesAsync();
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
            .ToList() ?? []);
}
