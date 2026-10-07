namespace EasyVetClinic.Api.Data;

public sealed class CatalogService
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "service";
    public decimal UnitPrice { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsArchived { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class BillingAccount
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string? PatientId { get; set; }
    public string? ConsultationId { get; set; }
    public string Currency { get; set; } = "MXN";
    public string Status { get; set; } = "Draft";
    public string? VoidReason { get; set; }
    public decimal Total { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public List<ChargeLine> Lines { get; } = [];
    public List<AccountPayment> Payments { get; } = [];
}

public sealed class ChargeLine
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string BillingAccountId { get; set; } = string.Empty;
    public string CatalogItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string NameSnapshot { get; set; } = string.Empty;
    public string SkuSnapshot { get; set; } = string.Empty;
    public string UnitSnapshot { get; set; } = string.Empty;
    public int CatalogVersion { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? PriceOverrideReason { get; set; }
    public decimal FulfilledQuantity { get; set; }
    public BillingAccount BillingAccount { get; set; } = null!;
}

public sealed class AccountPayment
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string BillingAccountId { get; set; } = string.Empty;
    public string Kind { get; set; } = "Payment";
    public string Method { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? CashReceived { get; set; }
    public decimal? ChangeGiven { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public BillingAccount BillingAccount { get; set; } = null!;
}

public sealed class InventoryMovement
{
    public string Id { get; set; } = string.Empty;
    public string ClinicId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductNameSnapshot { get; set; } = string.Empty;
    public string SkuSnapshot { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string? BillingAccountId { get; set; }
    public string? ChargeLineId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
