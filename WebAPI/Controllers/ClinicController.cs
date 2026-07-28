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
        var startOfDay = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = startOfDay.AddDays(1);
        var clinicAppointments = await dbContext.Appointments
            .Where(appointment => appointment.ClinicId == ClinicId)
            .Include(appointment => appointment.Patient)
            .ToListAsync();
        var appointments = clinicAppointments
            .Where(appointment => appointment.StartsAt >= startOfDay && appointment.StartsAt < endOfDay)
            .OrderBy(appointment => appointment.StartsAt)
            .ToList();

        return Ok(new DashboardSummary(
            clinic.Name,
            appointments.Count,
            await dbContext.Patients.CountAsync(patient => patient.ClinicId == ClinicId),
            0,
            appointments.Take(3).Select(appointment => new AppointmentSummary(
                appointment.StartsAt.ToString("HH:mm"),
                appointment.Patient.Name,
                appointment.Reason,
                appointment.ClinicianName)).ToList()));
    }

    [HttpGet("patients")]
    public async Task<ActionResult<IReadOnlyList<PatientSummary>>> GetPatients([FromQuery] string? query)
    {
        var patients = dbContext.Patients
            .AsNoTracking()
            .Include(patient => patient.Guardian)
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
        var patient = await dbContext.Patients.AsNoTracking().Include(patient => patient.Guardian)
            .SingleOrDefaultAsync(patient => patient.ClinicId == ClinicId && patient.Id == patientId);
        return patient is null ? NotFound() : Ok(MapPatient(patient));
    }

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

        var guardian = new Guardian
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim()
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

        var guardian = await dbContext.Guardians
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == guardianId);
        if (guardian is null)
        {
            return NotFound();
        }

        guardian.Name = request.Name.Trim();
        guardian.Phone = request.Phone.Trim();
        guardian.AlternatePhone = request.AlternatePhone.Trim();
        guardian.Address = request.Address.Trim();
        guardian.IdentityType = request.IdentityType.Trim();
        guardian.IdentityNumber = request.IdentityNumber.Trim();
        guardian.IdentityDocumentUrl = request.IdentityDocumentUrl.Trim();
        await dbContext.SaveChangesAsync();
        return Ok(MapGuardian(guardian));
    }

    [HttpPost("patients")]
    public async Task<ActionResult<PatientSummary>> CreatePatient(CreatePatientRequest request)
    {
        if (new[] { request.Name, request.Species, request.Breed, request.Sex, request.Weight, request.Color }.Any(string.IsNullOrWhiteSpace))
        {
            return ValidationProblem("Name, species, breed, sex, weight, and color are required.");
        }

        var guardian = await dbContext.Guardians
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == request.GuardianId);
        if (guardian is null)
        {
            return NotFound();
        }

        var patient = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            GuardianId = guardian.Id,
            Name = request.Name.Trim(),
            Species = request.Species.Trim(),
            Breed = request.Breed.Trim(),
            Sex = request.Sex.Trim(),
            Weight = request.Weight.Trim(),
            Color = request.Color.Trim(),
            Allergies = request.Allergies.Trim()
        };
        patient.Guardian = guardian;
        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync();
        return Created($"api/patients/{patient.Id}", MapPatient(patient));
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

    [HttpGet("appointments")]
    public async Task<ActionResult<IReadOnlyList<ScheduleAppointment>>> GetAppointments([FromQuery] DateOnly? date)
    {
        var selectedDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startOfDay = selectedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = startOfDay.AddDays(1);
        var clinicAppointments = await dbContext.Appointments
            .AsNoTracking()
            .Include(appointment => appointment.Patient)
            .Where(appointment => appointment.ClinicId == ClinicId)
            .ToListAsync();

        return Ok(clinicAppointments
            .Where(appointment => appointment.StartsAt >= startOfDay && appointment.StartsAt < endOfDay)
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

    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyList<ProductSummary>>> GetProducts()
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.ClinicId == ClinicId)
            .OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .Select(product => new ProductSummary(product.Id, product.Name, product.Category, product.UnitPrice, product.StockOnHand))
            .ToListAsync();
        return Ok(products);
    }

    [HttpPost("sales")]
    public async Task<ActionResult<SaleReceipt>> CompleteSale(CheckoutRequest request)
    {
        if (request.Lines.Count == 0 || string.IsNullOrWhiteSpace(request.PaymentMethod) || request.Lines.Any(line => line.Quantity <= 0))
        {
            return ValidationProblem("A payment method and at least one line with a positive quantity are required.");
        }

        if (!string.IsNullOrWhiteSpace(request.PatientId) && !await dbContext.Patients.AnyAsync(patient => patient.ClinicId == ClinicId && patient.Id == request.PatientId))
        {
            return NotFound();
        }

        var requestedQuantities = request.Lines
            .GroupBy(line => line.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
        var products = await dbContext.Products
            .Where(product => product.ClinicId == ClinicId && requestedQuantities.Keys.Contains(product.Id))
            .ToListAsync();

        if (products.Count != requestedQuantities.Count || products.Any(product => product.StockOnHand < requestedQuantities[product.Id]))
        {
            return Conflict("One or more products are unavailable in the requested quantity.");
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            PatientId = request.PatientId,
            CompletedAt = DateTimeOffset.UtcNow,
            PaymentMethod = request.PaymentMethod.Trim()
        };

        foreach (var product in products)
        {
            var quantity = requestedQuantities[product.Id];
            product.StockOnHand -= quantity;
            sale.Lines.Add(new SaleLine
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.UnitPrice,
                Quantity = quantity
            });
        }

        sale.Total = sale.Lines.Sum(line => line.UnitPrice * line.Quantity);
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        return Created($"api/sales/{sale.Id}", new SaleReceipt(
            sale.Id,
            sale.CompletedAt,
            sale.Total,
            sale.PaymentMethod,
            sale.Lines.Select(line => new SaleReceiptLine(line.ProductName, line.Quantity, line.UnitPrice)).ToList()));
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
        patient.IsActive);

    private static GuardianSummary MapGuardian(Guardian guardian) => new(
        guardian.Id,
        guardian.Name,
        guardian.Phone,
        guardian.AlternatePhone,
        guardian.Address,
        guardian.IdentityType,
        guardian.IdentityNumber,
        guardian.IdentityDocumentUrl);

    private static ClinicProfile MapClinicProfile(Clinic clinic) => new(
        clinic.Name,
        clinic.Address,
        clinic.LogoUrl,
        clinic.VeterinarianName,
        clinic.VeterinarianTitles,
        clinic.VeterinarianLicenseNumber);

    private static ScheduleAppointment MapAppointment(Appointment appointment) =>
        MapAppointment(appointment, appointment.Patient.Name);

    private static ScheduleAppointment MapAppointment(Appointment appointment, string patientName) => new(
        appointment.Id,
        appointment.PatientId,
        patientName,
        appointment.StartsAt,
        appointment.Reason,
        appointment.ClinicianName);

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