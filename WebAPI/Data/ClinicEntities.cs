namespace EasyVetClinic.Api.Data;

public sealed class Clinic
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<Guardian> Guardians { get; } = [];
    public List<Patient> Patients { get; } = [];
    public List<Appointment> Appointments { get; } = [];
    public List<Product> Products { get; } = [];
    public List<Sale> Sales { get; } = [];
}

public sealed class Guardian
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Clinic Clinic { get; set; } = null!;
    public List<Patient> Patients { get; } = [];
}

public sealed class Patient
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string GuardianId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string Breed { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Allergies { get; set; } = string.Empty;
    public DateOnly? LastVisit { get; set; }
    public Clinic Clinic { get; set; } = null!;
    public Guardian Guardian { get; set; } = null!;
    public List<Consultation> Consultations { get; } = [];
    public List<Appointment> Appointments { get; } = [];
}

public sealed class Appointment
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ClinicianName { get; set; } = string.Empty;
    public Clinic Clinic { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public sealed class Consultation
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string ClinicianName { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ChiefComplaint { get; set; } = string.Empty;
    public string ClinicalNotes { get; set; } = string.Empty;
    public Patient Patient { get; set; } = null!;
}

public sealed class Product
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int StockOnHand { get; set; }
    public Clinic Clinic { get; set; } = null!;
}

public sealed class Sale
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public Clinic Clinic { get; set; } = null!;
    public Patient? Patient { get; set; }
    public List<SaleLine> Lines { get; } = [];
}

public sealed class SaleLine
{
    public string Id { get; set; } = string.Empty;
    public string SaleId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public Sale Sale { get; set; } = null!;
}