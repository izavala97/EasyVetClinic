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
public sealed class PatientsController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

    [HttpGet("patients")]
    public async Task<ActionResult<IReadOnlyList<PatientSummary>>> GetPatients([FromQuery] string? query)
    {
        var patients = dbContext.Patients
            .AsNoTracking()
            .Include(patient => patient.Guardian)
            .Include(patient => patient.PatientGuardians)
            .ThenInclude(link => link.Guardian)
            .Where(patient => patient.ClinicId == ClinicId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            patients = patients.Where(patient =>
                patient.Name.Contains(query) ||
                patient.Guardian.Name.Contains(query) ||
                patient.Guardian.Phone.Contains(query));
        }

        var patientRecords = await patients.OrderBy(patient => patient.Name).ToListAsync();
        return Ok(patientRecords.Select(MapPatient).ToList());
    }

    [HttpGet("patients/{patientId}")]
    public async Task<ActionResult<PatientSummary>> GetPatient(string patientId)
    {
        var patient = await dbContext.Patients.AsNoTracking()
            .Include(patient => patient.Guardian)
            .Include(patient => patient.PatientGuardians)
            .ThenInclude(link => link.Guardian)
            .SingleOrDefaultAsync(patient => patient.ClinicId == ClinicId && patient.Id == patientId);
        return patient is null ? NotFound() : Ok(MapPatient(patient));
    }

    [HttpPost("patients")]
    public async Task<ActionResult<PatientSummary>> CreatePatient(CreatePatientRequest request)
    {
        if (new[] { request.Name, request.Species, request.Breed, request.Sex, request.Weight, request.Color }.Any(string.IsNullOrWhiteSpace))
        {
            return ValidationProblem("Name, species, breed, sex, weight, and color are required.");
        }

        var guardianIds = request.GuardianIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (guardianIds.Count == 0)
        {
            return ValidationProblem("At least one guardian is required.");
        }

        var guardians = await dbContext.Guardians
            .Where(candidate => candidate.ClinicId == ClinicId && guardianIds.Contains(candidate.Id))
            .ToListAsync();
        if (guardians.Count != guardianIds.Count)
        {
            return NotFound();
        }

        var patient = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            GuardianId = guardians[0].Id,
            Name = request.Name.Trim(),
            Species = request.Species.Trim(),
            Breed = request.Breed.Trim(),
            Sex = request.Sex.Trim(),
            Weight = request.Weight.Trim(),
            Color = request.Color.Trim(),
            Allergies = request.Allergies.Trim()
        };
        patient.Guardian = guardians[0];
        dbContext.Patients.Add(patient);
        dbContext.PatientGuardians.AddRange(guardians.Select(guardian => new PatientGuardian
        {
            PatientId = patient.Id,
            GuardianId = guardian.Id
        }));
        await dbContext.SaveChangesAsync();
        return Created($"api/patients/{patient.Id}", MapPatient(patient));
    }

    [HttpPost("patients/{patientId}/guardians/{guardianId}")]
    public async Task<IActionResult> AddGuardianToPatient(string patientId, string guardianId)
    {
        var patientExists = await dbContext.Patients.AnyAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == patientId);
        var guardianExists = await dbContext.Guardians.AnyAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == guardianId);
        if (!patientExists || !guardianExists)
        {
            return NotFound();
        }

        if (!await dbContext.PatientGuardians.AnyAsync(link => link.PatientId == patientId && link.GuardianId == guardianId))
        {
            dbContext.PatientGuardians.Add(new PatientGuardian { PatientId = patientId, GuardianId = guardianId });
            await dbContext.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpDelete("patients/{patientId}/guardians/{guardianId}")]
    public async Task<IActionResult> RemoveGuardianFromPatient(string patientId, string guardianId)
    {
        var patient = await dbContext.Patients
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == patientId);
        if (patient is null || patient.GuardianId == guardianId)
        {
            return NotFound();
        }

        var link = await dbContext.PatientGuardians
            .SingleOrDefaultAsync(candidate => candidate.PatientId == patientId && candidate.GuardianId == guardianId);
        if (link is null)
        {
            return NotFound();
        }

        dbContext.PatientGuardians.Remove(link);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("patients/{patientId}")]
    public async Task<ActionResult<PatientSummary>> UpdatePatient(string patientId, UpdatePatientRequest request)
    {
        if (new[] { request.Name, request.Species, request.Breed, request.Sex, request.Weight, request.Color }.Any(string.IsNullOrWhiteSpace))
        {
            return ValidationProblem("Name, species, breed, sex, weight, and color are required.");
        }

        var patient = await dbContext.Patients.Include(candidate => candidate.Guardian)
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == patientId);
        if (patient is null)
        {
            return NotFound();
        }

        patient.Name = request.Name.Trim();
        patient.Species = request.Species.Trim();
        patient.Breed = request.Breed.Trim();
        patient.Sex = request.Sex.Trim();
        patient.Weight = request.Weight.Trim();
        patient.Color = request.Color.Trim();
        patient.Allergies = request.Allergies.Trim();
        patient.DistinguishingFeatures = request.DistinguishingFeatures.Trim();
        patient.DateOfBirth = request.DateOfBirth;
        patient.PhotoUrl = request.PhotoUrl.Trim();
        patient.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync();
        return Ok(MapPatient(patient));
    }

    [HttpGet("patients/{patientId}/weights")]
    public async Task<ActionResult<IReadOnlyList<WeightRecordSummary>>> GetWeightRecords(string patientId)
    {
        var records = await dbContext.WeightRecords
            .AsNoTracking()
            .Where(record => record.ClinicId == ClinicId && record.PatientId == patientId)
            .OrderByDescending(record => record.MeasuredOn)
            .Select(record => new WeightRecordSummary(record.Id, record.Value, record.Unit, record.MeasuredOn, record.RecordedBy))
            .ToListAsync();
        return Ok(records);
    }

    [HttpPost("patients/{patientId}/weights")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.Veterinarian)]
    public async Task<ActionResult<WeightRecordSummary>> CreateWeightRecord(string patientId, CreateWeightRecordRequest request)
    {
        if (request.Value <= 0 || string.IsNullOrWhiteSpace(request.Unit) || string.IsNullOrWhiteSpace(request.RecordedBy))
        {
            return ValidationProblem("A positive weight, unit, and recorded-by value are required.");
        }

        var patient = await dbContext.Patients.SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == patientId);
        if (patient is null)
        {
            return NotFound();
        }

        var record = new WeightRecord
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            PatientId = patient.Id,
            Value = request.Value,
            Unit = request.Unit.Trim(),
            MeasuredOn = request.MeasuredOn,
            RecordedBy = request.RecordedBy.Trim()
        };
        patient.Weight = $"{record.Value:0.##} {record.Unit}";
        dbContext.WeightRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return Created($"api/patients/{patient.Id}/weights/{record.Id}", new WeightRecordSummary(record.Id, record.Value, record.Unit, record.MeasuredOn, record.RecordedBy));
    }

    [HttpGet("patients/{patientId}/vaccinations")]
    public async Task<ActionResult<IReadOnlyList<VaccinationRecordSummary>>> GetVaccinationRecords(string patientId)
    {
        var records = await dbContext.VaccinationRecords
            .AsNoTracking()
            .Where(record => record.ClinicId == ClinicId && record.PatientId == patientId)
            .OrderByDescending(record => record.AdministeredOn)
            .Select(record => new VaccinationRecordSummary(record.Id, record.VaccineName, record.AdministeredOn, record.NextDueOn, record.LotNumber, record.VeterinarianName))
            .ToListAsync();
        return Ok(records);
    }

    [HttpPost("patients/{patientId}/vaccinations")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin, ClinicRoles.Veterinarian)]
    public async Task<ActionResult<VaccinationRecordSummary>> CreateVaccinationRecord(string patientId, CreateVaccinationRecordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VaccineName) || string.IsNullOrWhiteSpace(request.VeterinarianName))
        {
            return ValidationProblem("Vaccine name and veterinarian name are required.");
        }

        if (!await dbContext.Patients.AnyAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == patientId))
        {
            return NotFound();
        }

        var record = new VaccinationRecord
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            PatientId = patientId,
            VaccineName = request.VaccineName.Trim(),
            AdministeredOn = request.AdministeredOn,
            NextDueOn = request.NextDueOn,
            LotNumber = request.LotNumber.Trim(),
            VeterinarianName = request.VeterinarianName.Trim()
        };
        dbContext.VaccinationRecords.Add(record);
        await dbContext.SaveChangesAsync();
        return Created($"api/patients/{patientId}/vaccinations/{record.Id}", new VaccinationRecordSummary(record.Id, record.VaccineName, record.AdministeredOn, record.NextDueOn, record.LotNumber, record.VeterinarianName));
    }

    [HttpGet("patients/{patientId}/consultations")]
    public async Task<ActionResult<IReadOnlyList<ConsultationHistoryItem>>> GetConsultationHistory(string patientId)
    {
        var records = await dbContext.Consultations
            .AsNoTracking()
            .Where(consultation => consultation.ClinicId == ClinicId && consultation.PatientId == patientId)
            .Select(consultation => new ConsultationHistoryItem(consultation.Id, consultation.StartedAt, consultation.ClinicianName, consultation.Status, consultation.Diagnosis))
            .ToListAsync();
        return Ok(records.OrderByDescending(consultation => consultation.StartedAt).ToList());
    }

    [HttpGet("patients/{patientId}/documents/{documentType}")]
    public async Task<ActionResult<DocumentDraft>> GetDocumentDraft(string patientId, string documentType)
    {
        var patient = await dbContext.Patients.AsNoTracking().Include(patient => patient.Guardian)
            .SingleOrDefaultAsync(patient => patient.ClinicId == ClinicId && patient.Id == patientId);
        if (patient is null)
        {
            return NotFound();
        }

        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["health-certificate"] = "Health certificate",
            ["surgical-consent"] = "Informed consent for anesthesia and surgery",
            ["boarding-contract"] = "Pet boarding agreement"
        };

        if (!titles.TryGetValue(documentType, out var title))
        {
            return NotFound();
        }

        var fields = new Dictionary<string, string>
        {
            ["Patient"] = patient.Name,
            ["Species"] = patient.Species,
            ["Breed"] = patient.Breed,
            ["Guardian"] = patient.Guardian.Name,
            ["Phone"] = patient.Guardian.Phone,
            ["Weight"] = patient.Weight,
            ["Date"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        };

        return Ok(new DocumentDraft(documentType, title, patient.Name, patient.Guardian.Name, "MVZ. Alondra Licona", fields));
    }

    private static PatientSummary MapPatient(Patient patient) => new(
        patient.Id,
        patient.GuardianId,
        patient.Name,
        patient.Species,
        patient.Breed,
        patient.Sex,
        patient.Weight,
        patient.Guardian.Name,
        patient.Guardian.Phone,
        patient.Color,
        patient.Allergies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        patient.LastVisit?.ToString("yyyy-MM-dd") ?? string.Empty,
        patient.DistinguishingFeatures,
        patient.DateOfBirth,
        patient.PhotoUrl,
        patient.IsActive,
        patient.PatientGuardians.Count > 0
            ? patient.PatientGuardians.Select(link => MapGuardian(link.Guardian)).OrderBy(guardian => guardian.Name).ToList()
            : [MapGuardian(patient.Guardian)]);

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
