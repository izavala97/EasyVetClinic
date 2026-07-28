namespace EasyVetClinic.Api.Models;

public sealed record PatientSummary(
    string Id,
    string Name,
    string Species,
    string Breed,
    string Sex,
    string Weight,
    string GuardianName,
    string GuardianPhone,
    string Color,
    IReadOnlyList<string> Allergies,
    string LastVisit);

public sealed record GuardianSummary(string Id, string Name, string Phone);

public sealed record CreateGuardianRequest(string Name, string Phone);

public sealed record CreatePatientRequest(
    string GuardianId,
    string Name,
    string Species,
    string Breed,
    string Sex,
    string Weight,
    string Color,
    string Allergies);

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
    string ClinicalNotes);

public sealed record UpdateConsultationRequest(string ChiefComplaint, string ClinicalNotes, string Status);

public sealed record DocumentDraft(
    string Type,
    string Title,
    string PatientName,
    string GuardianName,
    string PreparedBy,
    IReadOnlyDictionary<string, string> Fields);