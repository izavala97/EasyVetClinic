namespace EasyVetClinic.Api.Models;

public sealed record PatientSummary(
    string Id,
    string GuardianId,
    string Name,
    string Species,
    string Breed,
    string Sex,
    string Weight,
    string GuardianName,
    string GuardianPhone,
    string Color,
    IReadOnlyList<string> Allergies,
    string LastVisit,
    string DistinguishingFeatures,
    DateOnly? DateOfBirth,
    string PhotoUrl,
    bool IsActive);

public sealed record GuardianSummary(
    string Id,
    string Name,
    string Phone,
    string AlternatePhone,
    string Address,
    string IdentityType,
    string IdentityNumber,
    string IdentityDocumentUrl);

public sealed record CreateGuardianRequest(string Name, string Phone);

public sealed record UpdateGuardianRequest(
    string Name,
    string Phone,
    string AlternatePhone,
    string Address,
    string IdentityType,
    string IdentityNumber,
    string IdentityDocumentUrl);

public sealed record CreatePatientRequest(
    string GuardianId,
    string Name,
    string Species,
    string Breed,
    string Sex,
    string Weight,
    string Color,
    string Allergies);

public sealed record UpdatePatientRequest(
    string Name,
    string Species,
    string Breed,
    string Sex,
    string Weight,
    string Color,
    string Allergies,
    string DistinguishingFeatures,
    DateOnly? DateOfBirth,
    string PhotoUrl,
    bool IsActive);

public sealed record WeightRecordSummary(string Id, decimal Value, string Unit, DateOnly MeasuredOn, string RecordedBy);

public sealed record CreateWeightRecordRequest(decimal Value, string Unit, DateOnly MeasuredOn, string RecordedBy);

public sealed record VaccinationRecordSummary(string Id, string VaccineName, DateOnly AdministeredOn, DateOnly? NextDueOn, string LotNumber, string VeterinarianName);

public sealed record CreateVaccinationRecordRequest(string VaccineName, DateOnly AdministeredOn, DateOnly? NextDueOn, string LotNumber, string VeterinarianName);

public sealed record ConsultationHistoryItem(string Id, DateTimeOffset StartedAt, string ClinicianName, string Status, string Diagnosis);

public sealed record ConsultationListItem(
    string Id,
    string PatientId,
    string PatientName,
    string GuardianName,
    string ClinicianName,
    DateTimeOffset StartedAt,
    string Status,
    string Diagnosis);

public sealed record AppointmentSummary(string Time, string PatientName, string Reason, string ClinicianName);

public sealed record ScheduleAppointment(
    string Id,
    string PatientId,
    string PatientName,
    DateTimeOffset StartsAt,
    string Reason,
    string ClinicianName);

public sealed record CreateAppointmentRequest(
    string PatientId,
    DateTimeOffset StartsAt,
    string Reason,
    string ClinicianName);

public sealed record ProductSummary(
    string Id,
    string Name,
    string Category,
    decimal UnitPrice,
    int StockOnHand);

public sealed record CheckoutLineRequest(string ProductId, int Quantity);

public sealed record CheckoutRequest(
    string? PatientId,
    string PaymentMethod,
    IReadOnlyList<CheckoutLineRequest> Lines);

public sealed record SaleReceipt(
    string Id,
    DateTimeOffset CompletedAt,
    decimal Total,
    string PaymentMethod,
    IReadOnlyList<SaleReceiptLine> Lines);

public sealed record SaleReceiptLine(string ProductName, int Quantity, decimal UnitPrice);

public sealed record DashboardSummary(
    string ClinicName,
    int TodayAppointments,
    int ActivePatients,
    int BoardingGuests,
    IReadOnlyList<AppointmentSummary> UpcomingAppointments);

public sealed record ClinicProfile(
    string Name,
    string Address,
    string LogoUrl,
    string VeterinarianName,
    string VeterinarianTitles,
    string VeterinarianLicenseNumber);

public sealed record UpdateClinicProfileRequest(
    string Name,
    string Address,
    string LogoUrl,
    string VeterinarianName,
    string VeterinarianTitles,
    string VeterinarianLicenseNumber);

public sealed record ConsultationSummary(
    string Id,
    string PatientId,
    string PatientName,
    string ClinicianName,
    DateTimeOffset StartedAt,
    string Status);

public sealed record StartConsultationRequest(string ClinicianName);

public sealed record ConsultationDetail(
    string Id,
    string PatientId,
    string PatientName,
    string GuardianName,
    string ClinicianName,
    DateTimeOffset StartedAt,
    string Status,
    string ChiefComplaint,
    string ClinicalNotes,
    string Diagnosis,
    string Instructions,
    DateTimeOffset? PrescriptionLastUpdatedAt,
    IReadOnlyList<PrescriptionItemSummary> PrescriptionItems);

public sealed record PrescriptionItemSummary(string Id, string MedicationName, string Presentation, string Concentration, string DosageDirections, int SortOrder);

public sealed record UpdateConsultationRequest(
    string ChiefComplaint,
    string ClinicalNotes,
    string Diagnosis,
    string Instructions,
    string Status,
    IReadOnlyList<PrescriptionItemRequest> PrescriptionItems);

public sealed record PrescriptionItemRequest(string MedicationName, string Presentation, string Concentration, string DosageDirections, int SortOrder);

public sealed record DocumentDraft(
    string Type,
    string Title,
    string PatientName,
    string GuardianName,
    string PreparedBy,
    IReadOnlyDictionary<string, string> Fields);

public sealed record CurrentUserSummary(string ObjectId, string DisplayName, string? ClinicId, string? ClinicName, string? Role);

public sealed record CreateInitialClinicRequest(
    string Name,
    string Address,
    string LogoUrl,
    string VeterinarianName,
    string VeterinarianTitles,
    string VeterinarianLicenseNumber);