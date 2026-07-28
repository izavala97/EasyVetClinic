namespace EasyVetClinic.Api.Data;

public sealed class Clinic
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string VeterinarianName { get; set; } = string.Empty;
    public string VeterinarianTitles { get; set; } = string.Empty;
    public string VeterinarianLicenseNumber { get; set; } = string.Empty;
    public List<Guardian> Guardians { get; } = [];
    public List<ClinicUser> Users { get; } = [];
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
    public string AlternatePhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string IdentityType { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string IdentityDocumentUrl { get; set; } = string.Empty;
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
    public string DistinguishingFeatures { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateOnly? LastVisit { get; set; }
    public Clinic Clinic { get; set; } = null!;
    public Guardian Guardian { get; set; } = null!;
    public List<Consultation> Consultations { get; } = [];
    public List<Appointment> Appointments { get; } = [];
    public List<WeightRecord> WeightRecords { get; } = [];
    public List<VaccinationRecord> VaccinationRecords { get; } = [];
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
    public string Diagnosis { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public Patient Patient { get; set; } = null!;
    public Prescription? Prescription { get; set; }
}

public sealed class WeightRecord
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = "kg";
    public DateOnly MeasuredOn { get; set; }
    public string RecordedBy { get; set; } = string.Empty;
    public Patient Patient { get; set; } = null!;
}

public sealed class VaccinationRecord
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string VaccineName { get; set; } = string.Empty;
    public DateOnly AdministeredOn { get; set; }
    public DateOnly? NextDueOn { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public string VeterinarianName { get; set; } = string.Empty;
    public Patient Patient { get; set; } = null!;
}

public sealed class Prescription
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string ConsultationId { get; set; } = string.Empty;
    public string DiagnosisSnapshot { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public bool IsFinalized { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
    public Consultation Consultation { get; set; } = null!;
    public List<PrescriptionItem> Items { get; } = [];
}

public sealed class PrescriptionItem
{
    public string Id { get; set; } = string.Empty;
    public string PrescriptionId { get; set; } = string.Empty;
    public string MedicationName { get; set; } = string.Empty;
    public string Presentation { get; set; } = string.Empty;
    public string Concentration { get; set; } = string.Empty;
    public string DosageDirections { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public Prescription Prescription { get; set; } = null!;
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

public sealed class ClinicUser
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string EntraObjectId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = ClinicRoles.Staff;
    public bool IsActive { get; set; } = true;
    public Clinic Clinic { get; set; } = null!;
}

public static class ClinicRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ClinicAdmin = "ClinicAdmin";
    public const string Veterinarian = "Veterinarian";
    public const string Staff = "Staff";
}