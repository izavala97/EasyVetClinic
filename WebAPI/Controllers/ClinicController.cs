using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class ClinicController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

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
            .Select(guardian => new GuardianSummary(guardian.Id, guardian.Name, guardian.Phone))
            .ToListAsync();
        return Ok(guardians);
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
        return Created($"api/guardians/{guardian.Id}", new GuardianSummary(guardian.Id, guardian.Name, guardian.Phone));
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
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == consultationId);
        return consultation is null ? NotFound() : Ok(MapConsultation(consultation));
    }

    [HttpPut("consultations/{consultationId}")]
    public async Task<ActionResult<ConsultationDetail>> UpdateConsultation(string consultationId, UpdateConsultationRequest request)
    {
        if (request.Status is not ("InProgress" or "Completed"))
        {
            return ValidationProblem("Status must be InProgress or Completed.");
        }

        var consultation = await dbContext.Consultations
            .Include(candidate => candidate.Patient)
            .ThenInclude(patient => patient.Guardian)
            .SingleOrDefaultAsync(candidate => candidate.ClinicId == ClinicId && candidate.Id == consultationId);
        if (consultation is null)
        {
            return NotFound();
        }

        consultation.ChiefComplaint = request.ChiefComplaint.Trim();
        consultation.ClinicalNotes = request.ClinicalNotes.Trim();
        consultation.Status = request.Status;
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
        patient.Name,
        patient.Species,
        patient.Breed,
        patient.Sex,
        patient.Weight,
        patient.Guardian.Name,
        patient.Guardian.Phone,
        patient.Color,
        patient.Allergies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        patient.LastVisit?.ToString("yyyy-MM-dd") ?? string.Empty);

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
        consultation.ClinicalNotes);
}