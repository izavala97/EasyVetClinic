namespace EasyVetClinic.Api.Models;

public sealed record ActiveConsultationSummary(
    string Id, string PatientId, string PatientName, string GuardianName,
    string ClinicianName, DateTimeOffset StartedAt, string Status, int Version, string? BillingAccountId);

public sealed record OutstandingCurrencySummary(string Currency, decimal OutstandingBalance);

public sealed record TodayOperationsSummary(
    DateOnly Date, string TimeZoneId, string Currency,
    int ActiveConsultations, int PendingAccounts, decimal OutstandingBalance, int LowStockProducts,
    IReadOnlyList<OutstandingCurrencySummary> BalancesByCurrency,
    IReadOnlyList<ActiveConsultationSummary> Consultations);

public sealed record CurrencyOperationsSummary(
    string Currency, decimal BilledTotal, decimal PaymentsReceived, decimal RefundsRecorded, decimal NetPayments);

public sealed record OperationsRangeSummary(
    DateOnly From, DateOnly To, string TimeZoneId, int OpenedAccounts, int PaymentCount, int RefundCount,
    IReadOnlyList<CurrencyOperationsSummary> TotalsByCurrency);
