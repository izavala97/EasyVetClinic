namespace EasyVetClinic.Api.Models;

public sealed record CatalogItemRequest(
    string Name,
    string Category,
    string? Description,
    string? Sku,
    string Unit,
    decimal UnitPrice,
    decimal StockOnHand,
    decimal LowStockThreshold,
    bool TrackStock,
    bool IsFavorite);

public sealed record CatalogItemSummary(
    string Id,
    string Type,
    string Name,
    string Category,
    string Description,
    string? Sku,
    string Unit,
    decimal UnitPrice,
    decimal StockOnHand,
    decimal LowStockThreshold,
    bool TrackStock,
    bool IsFavorite,
    bool IsArchived,
    int Version);

public sealed record CreateBillingAccountRequest(string? PatientId, string? ConsultationId, string? Currency);
public sealed record AddChargeLineRequest(string CatalogItemId, string ItemType, decimal Quantity, int ExpectedVersion, int? ExpectedCatalogVersion);
public sealed record UpdateChargeLineRequest(decimal Quantity, decimal? UnitPrice, int ExpectedVersion, string? PriceOverrideReason);
public sealed record AccountVersionRequest(int ExpectedVersion);
public sealed record PaymentRequest(decimal Amount, string Method, string IdempotencyKey, int ExpectedVersion, string? Note, decimal? CashReceived);
public sealed record RefundRequest(decimal Amount, string Method, string IdempotencyKey, int ExpectedVersion, string? Note);
public sealed record VoidAccountRequest(int ExpectedVersion, string Reason);
public sealed record InventoryAdjustmentRequest(string ProductId, decimal QuantityChange, string Reason, string IdempotencyKey);

public sealed record AccountLineSummary(
    string Id,
    string CatalogItemId,
    string ItemType,
    string Name,
    string Sku,
    string Unit,
    int CatalogVersion,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal FulfilledQuantity,
    string? PriceOverrideReason);

public sealed record AccountPaymentSummary(
    string Id,
    string Kind,
    string Method,
    decimal Amount,
    string Note,
    DateTimeOffset CreatedAt,
    decimal? CashReceived,
    decimal? ChangeGiven,
    string IdempotencyKey);

public sealed record BillingAccountSummary(
    string Id,
    string? PatientId,
    string? PatientName,
    string? ConsultationId,
    string Currency,
    string Status,
    string PaymentStatus,
    decimal Total,
    decimal AmountPaid,
    decimal BalanceDue,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? OpenedAt,
    DateTimeOffset? ClosedAt,
    string? VoidReason,
    IReadOnlyList<AccountLineSummary> Lines,
    IReadOnlyList<AccountPaymentSummary> Payments);

public sealed record InventoryMovementSummary(
    string Id,
    string ProductId,
    string ProductName,
    string Sku,
    string Type,
    decimal QuantityChange,
    decimal BalanceAfter,
    string Reason,
    string? BillingAccountId,
    string? ChargeLineId,
    DateTimeOffset CreatedAt);
