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
    bool IsActive,
    IReadOnlyList<GuardianSummary> Guardians);

public sealed record GuardianSummary(
    string Id,
    string Name,
    string Phone,
    string AlternatePhone,
    string Address,
    string IdentityType,
    string IdentityNumber,
    string IdentityDocumentUrl);

public sealed record CreateGuardianRequest(
    string Name,
    string Phone,
    string? AlternatePhone,
    string? Address,
    string? IdentityType,
    string? IdentityNumber,
    string? IdentityDocumentUrl);

public sealed record UpdateGuardianRequest(
    string Name,
    string Phone,
    string AlternatePhone,
    string Address,
    string IdentityType,
    string IdentityNumber,
    string IdentityDocumentUrl);

public sealed record CreatePatientRequest(
    IReadOnlyList<string>? GuardianIds,
    string Name,
    string Species,
    string? Breed = null,
    string? Sex = null,
    string? Weight = null,
    string? Color = null,
    string? Allergies = null,
    string? PrimaryGuardianId = null,
    CreateGuardianRequest? NewGuardian = null);

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

public sealed record AppointmentSummary(string Id, DateTimeOffset StartsAt, string Time, string PatientName, string Reason, string ClinicianName);

public sealed record ScheduleAppointment(
    string Id,
    string PatientId,
    string PatientName,
    DateTimeOffset StartsAt,
    string Reason,
    string ClinicianName,
    int DurationMinutes,
    string Status,
    string? ConsultationId,
    int Version,
    string ClinicianObjectId);

public sealed record CreateAppointmentRequest(
    string PatientId,
    DateTimeOffset StartsAt,
    string Reason,
    string ClinicianName,
    int DurationMinutes = 30,
    string? ClinicianObjectId = null,
    string? LocalStartsAt = null);

public sealed record UpdateAppointmentRequest(
    string PatientId,
    DateTimeOffset StartsAt,
    string Reason,
    string ClinicianName,
    int DurationMinutes = 30,
    string Status = "Scheduled",
    int? ExpectedVersion = null,
    string? ClinicianObjectId = null,
    string? LocalStartsAt = null);

public sealed record ProductSummary(
    string Id,
    string Name,
    string Category,
    decimal UnitPrice,
    decimal StockOnHand);

public sealed record CheckoutLineRequest(string ProductId, int Quantity);
public sealed record ServiceSaleLineRequest(string Name, decimal UnitPrice);

public sealed record CheckoutRequest(
    string? PatientId,
    string PaymentMethod,
    IReadOnlyList<CheckoutLineRequest> Lines,
    IReadOnlyList<ServiceSaleLineRequest> ServiceLines);

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
    string TimeZoneId,
    string Language);

public sealed record UpdateClinicProfileRequest(
    string Name,
    string Address,
    string LogoUrl,
    string TimeZoneId = "UTC",
    string? Language = null);

public sealed record ConsultationSummary(
    string Id,
    string PatientId,
    string PatientName,
    string ClinicianName,
    DateTimeOffset StartedAt,
    string Status);

public sealed record StartConsultationRequest(string? ClinicianName, string? AppointmentId = null, string? ClinicianObjectId = null);

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
    IReadOnlyList<PrescriptionItemSummary> PrescriptionItems,
    int Version,
    string ClinicianTitles,
    string ClinicianLicenseNumber,
    string CancellationReason,
    IReadOnlyList<ClinicalAmendmentSummary> Amendments);

public sealed record ClinicalAmendmentSummary(string Id, string AuthorName, string Reason, string Note, DateTimeOffset CreatedAt);
public sealed record CreateClinicalAmendmentRequest(string Reason, string Note, int ExpectedVersion);
public sealed record CancelConsultationRequest(string Reason, int ExpectedVersion);
public sealed record ClinicianSummary(string ObjectId, string DisplayName, string ProfessionalTitles, string LicenseNumber, bool CanEditCredentials = true);
public sealed record UpdateClinicianCredentialsRequest(string ProfessionalTitles, string LicenseNumber);

public sealed record PrescriptionItemSummary(string Id, string MedicationName, string Presentation, string Concentration, string DosageDirections, int SortOrder);

public sealed record UpdateConsultationRequest(
    string ChiefComplaint,
    string ClinicalNotes,
    string Diagnosis,
    string Instructions,
    string Status,
    IReadOnlyList<PrescriptionItemRequest> PrescriptionItems,
    int? ExpectedVersion = null);

public sealed record PrescriptionItemRequest(string MedicationName, string Presentation, string Concentration, string DosageDirections, int SortOrder, string? Id = null);

public sealed record DocumentDraft(
    string Type,
    string Title,
    string PatientName,
    string GuardianName,
    string PreparedBy,
    IReadOnlyDictionary<string, string> Fields);

public sealed record CurrentUserSummary(string ObjectId, string DisplayName, string? ClinicId, string? ClinicName,
    string? Role, bool IsPlatformOwner, bool IsSuspended, IReadOnlyList<ClinicMembershipSummary> Clinics);

public sealed record ClinicMembershipSummary(string ClinicId, string ClinicName, string Role, bool IsSuspended);
public sealed record PlatformClinicSummary(string Id, string Name, bool IsSuspended, string SuspensionReason, int UserCount);
public sealed record ClinicUserSummary(string Id, string DisplayName, string Role, bool IsActive);
public sealed record UpdateClinicAccessRequest(bool IsSuspended, string Reason);
public sealed record UpdateClinicUserRequest(string Role, bool IsActive);
public sealed record CreateInvitationRequest(string Email, string Role);
public sealed record AcceptInvitationRequest(string Token);
public sealed record InvitationSummary(string Id, string Email, string Role, DateTimeOffset ExpiresAt, bool IsUsed, bool IsRevoked);
public sealed record CreatedInvitation(InvitationSummary Invitation, string Token);

public sealed record CreateInitialClinicRequest(
    string Name,
    string Address,
    string LogoUrl);