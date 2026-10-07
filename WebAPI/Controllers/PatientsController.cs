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
public sealed class PatientsController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

    [HttpGet("patients")]
    public async Task<ActionResult<IReadOnlyList<PatientSummary>>> GetPatients([FromQuery] string? query, [FromQuery] int? skip, [FromQuery] int? take)
    {
        if (skip is < 0 || take is < 1 or > 200) return ValidationProblem("Skip must be non-negative and take must be between 1 and 200.");
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
                patient.Guardian.Phone.Contains(query) ||
                patient.PatientGuardians.Any(link => link.Guardian.Name.Contains(query) || link.Guardian.Phone.Contains(query)));
        }

        var ordered = patients.OrderBy(patient => patient.Name).ThenBy(patient => patient.Id).Skip(skip ?? 0);
        if (take is not null) ordered = ordered.Take(take.Value);
        var patientRecords = await ordered.ToListAsync();
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
        if (new[] { request.Name, request.Species }.Any(string.IsNullOrWhiteSpace))
        {
            return ValidationProblem("Name and species are required.");
        }

        var guardianIds = (request.GuardianIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (guardianIds.Count == 0 && request.NewGuardian is null)
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
        if (request.PrimaryGuardianId is not null && !guardianIds.Contains(request.PrimaryGuardianId))
            return ValidationProblem("The primary guardian must be among the linked guardians.");
        if (request.NewGuardian is not null)
        {
            if (string.IsNullOrWhiteSpace(request.NewGuardian.Name) || string.IsNullOrWhiteSpace(request.NewGuardian.Phone))
                return ValidationProblem("The new guardian requires a name and phone.");
            var phone = new string(request.NewGuardian.Phone.Where(char.IsDigit).ToArray());
            var alternatePhone = new string((request.NewGuardian.AlternatePhone ?? "").Where(char.IsDigit).ToArray());
            if (phone.Length == 0 || (!string.IsNullOrWhiteSpace(request.NewGuardian.AlternatePhone) && alternatePhone.Length == 0))
                return ValidationProblem("Guardian phone numbers must contain digits.");
            if (await dbContext.Guardians.AnyAsync(guardian => guardian.ClinicId == ClinicId && guardian.Phone == phone))
                return Conflict(new ProblemDetails { Title = "A guardian with this phone already exists. Select the existing contact." });
            var newGuardian = new Guardian
            {
                Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, Name = request.NewGuardian.Name.Trim(), Phone = phone,
                AlternatePhone = alternatePhone,
                Address = request.NewGuardian.Address?.Trim() ?? "",
                IdentityType = request.NewGuardian.IdentityType?.Trim() ?? "",
                IdentityNumber = request.NewGuardian.IdentityNumber?.Trim() ?? "",
                IdentityDocumentUrl = request.NewGuardian.IdentityDocumentUrl?.Trim() ?? ""
            };
            guardians.Add(newGuardian);
            guardianIds.Add(newGuardian.Id);
            dbContext.Guardians.Add(newGuardian);
        }
        var primaryGuardian = guardians.Single(guardian => guardian.Id == (request.PrimaryGuardianId ?? guardianIds[0]));

        var patient = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            GuardianId = primaryGuardian.Id,
            Name = request.Name.Trim(),
            Species = request.Species.Trim(),
            Breed = request.Breed?.Trim() ?? "",
            Sex = request.Sex?.Trim() ?? "Unknown",
            Weight = request.Weight?.Trim() ?? "",
            Color = request.Color?.Trim() ?? "",
            Allergies = request.Allergies?.Trim() ?? ""
        };
        patient.Guardian = primaryGuardian;
        dbContext.Patients.Add(patient);
        dbContext.PatientGuardians.AddRange(guardians.Select(guardian => new PatientGuardian
        {
            PatientId = patient.Id,
            GuardianId = guardian.Id
        }));
        await dbContext.SaveChangesAsync();
        return Created($"api/patients/{patient.Id}", MapPatient(patient));
    }

    [HttpPut("patients/{patientId}/primary-guardian/{guardianId}")]
    public async Task<IActionResult> SetPrimaryGuardian(string patientId, string guardianId)
    {
        var patient = await dbContext.Patients.SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == patientId);
        if (patient is null || !await dbContext.Guardians.AnyAsync(guardian => guardian.ClinicId == ClinicId && guardian.Id == guardianId) ||
            !await dbContext.PatientGuardians.AnyAsync(link => link.PatientId == patientId && link.GuardianId == guardianId))
            return NotFound();
        patient.GuardianId = guardianId;
        await dbContext.SaveChangesAsync();
        return NoContent();
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
        if (new[] { request.Name, request.Species }.Any(string.IsNullOrWhiteSpace))
        {
            return ValidationProblem("Name and species are required.");
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
        if (request.Value <= 0 || request.Unit is not ("kg" or "lb") || string.IsNullOrWhiteSpace(request.RecordedBy))
        {
            return ValidationProblem("A positive weight, kg or lb unit, and recorded-by value are required.");
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
        var latest = await dbContext.WeightRecords.Where(candidate => candidate.ClinicId == ClinicId && candidate.PatientId == patientId)
            .MaxAsync(candidate => (DateOnly?)candidate.MeasuredOn);
        if (latest is null || record.MeasuredOn >= latest)
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
        if (string.IsNullOrWhiteSpace(request.VaccineName) || string.IsNullOrWhiteSpace(request.VeterinarianName) ||
            request.NextDueOn < request.AdministeredOn)
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

        return Ok(new DocumentDraft(documentType, title, patient.Name, patient.Guardian.Name,
            User.FindFirst("name")?.Value ?? User.Identity?.Name ?? "Clinic staff", fields));
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
