using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api/billing/accounts")]
[Authorize]
[RequireClinicAccess]
public sealed class BillingController(ClinicDbContext db, CurrentClinic clinic) : ControllerBase
{
    private const decimal MaximumMoney = 9999999999999999.99m;
    private const decimal MaximumQuantity = 999999999999999.999m;
    private string ClinicId => clinic.Id;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BillingAccountSummary>>> GetAccounts([FromQuery] string? consultationId)
    {
        if (consultationId is not null && !await db.Consultations.AsNoTracking()
            .AnyAsync(item => item.ClinicId == ClinicId && item.Id == consultationId))
            return NotFound(new { error = "Consultation not found in this clinic." });
        var accounts = await db.BillingAccounts.AsNoTracking()
            .AsSplitQuery()
            .Where(account => account.ClinicId == ClinicId)
            .Where(account => consultationId == null || account.ConsultationId == consultationId)
            .Include(account => account.Lines)
            .Include(account => account.Payments)
            .OrderByDescending(account => account.CreatedAt.ToString())
            .Take(200)
            .ToListAsync();
        var patients = await db.Patients.AsNoTracking().Where(patient => patient.ClinicId == ClinicId)
            .ToDictionaryAsync(patient => patient.Id, patient => patient.Name);
        return Ok(accounts.Select(account => ToSummary(account, patients.GetValueOrDefault(account.PatientId ?? string.Empty))));
    }

    [HttpPost]
    public async Task<ActionResult<BillingAccountSummary>> CreateAccount(CreateBillingAccountRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Currency) &&
            (request.Currency.Trim().Length != 3 || request.Currency.Any(character => !char.IsAsciiLetter(character))))
            return ValidationProblem("Currency must be a three-letter ISO code.");
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "MXN" : request.Currency.Trim().ToUpperInvariant();
        Patient? patient = null;
        if (!string.IsNullOrWhiteSpace(request.ConsultationId))
        {
            var consultation = await db.Consultations.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == request.ConsultationId);
            if (consultation is null) return NotFound(new { error = "Consultation not found in this clinic." });
            if (!string.IsNullOrWhiteSpace(request.PatientId) && request.PatientId != consultation.PatientId)
                return ValidationProblem("The patient must match the linked consultation.");
            patient = await db.Patients.SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == consultation.PatientId);
            var existing = await LoadConsultationAccount(consultation.Id);
            if (existing is not null) return ExistingConsultationAccount(existing, currency, patient?.Name);
        }
        else if (!string.IsNullOrWhiteSpace(request.PatientId))
        {
            patient = await db.Patients.SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == request.PatientId);
        }
        if (!string.IsNullOrWhiteSpace(request.PatientId) && patient is null)
            return NotFound(new { error = "Patient not found in this clinic." });

        var now = DateTimeOffset.UtcNow;
        var account = new BillingAccount
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, PatientId = patient?.Id,
            ConsultationId = string.IsNullOrWhiteSpace(request.ConsultationId) ? null : request.ConsultationId,
            Currency = currency,
            CreatedAt = now, UpdatedAt = now
        };
        db.BillingAccounts.Add(account);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (CommercialPersistence.IsUniqueViolation(exception) && account.ConsultationId is not null)
        {
            db.Entry(account).State = EntityState.Detached;
            var existing = await LoadConsultationAccount(account.ConsultationId);
            if (existing is not null) return ExistingConsultationAccount(existing, currency, patient?.Name);
            throw;
        }
        return CreatedAtAction(nameof(GetAccount), new { id = account.Id }, ToSummary(account, patient?.Name));
    }

    private Task<BillingAccount?> LoadConsultationAccount(string consultationId) =>
        db.BillingAccounts.AsNoTracking().AsSplitQuery()
            .Include(account => account.Lines).Include(account => account.Payments)
            .SingleOrDefaultAsync(account => account.ClinicId == ClinicId && account.ConsultationId == consultationId);

    private ActionResult<BillingAccountSummary> ExistingConsultationAccount(
        BillingAccount account, string currency, string? patientName) =>
        account.Currency == currency
            ? Ok(ToSummary(account, patientName))
            : Conflict(new { error = "This consultation already has a billing account with a different currency." });

    [HttpGet("{id}")]
    public async Task<ActionResult<BillingAccountSummary>> GetAccount(string id)
    {
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        return Ok(await MapAccount(account));
    }

    [HttpPost("{id}/lines")]
    public async Task<ActionResult<BillingAccountSummary>> AddLine(string id, AddChargeLineRequest request)
    {
        if (request.Quantity <= 0 || request.Quantity > MaximumQuantity ||
            request.Quantity != decimal.Round(request.Quantity, 3) || request.ExpectedVersion < 1)
            return ValidationProblem("A positive quantity and expectedVersion are required.");
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (account.Version != request.ExpectedVersion) return VersionConflict(account);
        if (!CanEdit(account)) return Conflict(new { error = "Only draft accounts can be edited." });
        string name;
        string sku;
        string unit;
        decimal price;
        int version;
        if (string.Equals(request.ItemType, "Product", StringComparison.OrdinalIgnoreCase))
        {
            var product = await db.Products.SingleOrDefaultAsync(item =>
                item.ClinicId == ClinicId && item.Id == request.CatalogItemId && !item.IsArchived);
            if (product is null) return NotFound(new { error = "Product not found in this clinic or archived." });
            if (request.ExpectedCatalogVersion.HasValue && product.Version != request.ExpectedCatalogVersion.Value)
                return Conflict(new { error = "Product catalog version changed.", currentVersion = product.Version });
            name = product.Name; sku = product.Sku ?? string.Empty; unit = product.Unit; price = product.UnitPrice; version = product.Version;
        }
        else if (string.Equals(request.ItemType, "Service", StringComparison.OrdinalIgnoreCase))
        {
            var service = await db.CatalogServices.SingleOrDefaultAsync(item =>
                item.ClinicId == ClinicId && item.Id == request.CatalogItemId && !item.IsArchived);
            if (service is null) return NotFound(new { error = "Service not found in this clinic or archived." });
            if (request.ExpectedCatalogVersion.HasValue && service.Version != request.ExpectedCatalogVersion.Value)
                return Conflict(new { error = "Service catalog version changed.", currentVersion = service.Version });
            name = service.Name; sku = string.Empty; unit = service.Unit; price = service.UnitPrice; version = service.Version;
        }
        else return ValidationProblem("ItemType must be Product or Service.");

        if ((price != 0 && request.Quantity > MaximumMoney / price) ||
            RecalculateTotal(account) > MaximumMoney - decimal.Round(request.Quantity * price, 2))
            return ValidationProblem("The resulting line or account total exceeds the supported currency precision.");
        account.Lines.Add(new ChargeLine
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, CatalogItemId = request.CatalogItemId,
            ItemType = request.ItemType.Equals("Product", StringComparison.OrdinalIgnoreCase) ? "Product" : "Service",
            NameSnapshot = name, SkuSnapshot = sku, UnitSnapshot = unit, CatalogVersion = version,
            Quantity = request.Quantity, UnitPrice = price, LineTotal = decimal.Round(request.Quantity * price, 2)
        });
        return await SaveDraft(account);
    }

    [HttpPut("{id}/lines/{lineId}")]
    public async Task<ActionResult<BillingAccountSummary>> UpdateLine(string id, string lineId, UpdateChargeLineRequest request)
    {
        if (request.Quantity <= 0 || request.Quantity > MaximumQuantity ||
            request.Quantity != decimal.Round(request.Quantity, 3) || request.ExpectedVersion < 1 ||
            (request.UnitPrice.HasValue && (request.UnitPrice < 0 || request.UnitPrice > MaximumMoney)))
            return ValidationProblem("A positive quantity, non-negative optional unit price, and expectedVersion are required.");
        if (request.UnitPrice.HasValue && clinic.Role != ClinicRoles.SuperAdmin && clinic.Role != ClinicRoles.ClinicAdmin)
            return Forbid();
        if (request.UnitPrice.HasValue &&
            (string.IsNullOrWhiteSpace(request.PriceOverrideReason) || request.PriceOverrideReason.Trim().Length > 500))
            return ValidationProblem("A price override requires a reason of at most 500 characters.");
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (account.Version != request.ExpectedVersion) return VersionConflict(account);
        if (!CanEdit(account)) return Conflict(new { error = "Only draft accounts can be edited." });
        var line = account.Lines.SingleOrDefault(item => item.ClinicId == ClinicId && item.Id == lineId);
        if (line is null) return NotFound();
        if (line.FulfilledQuantity != 0) return Conflict(new { error = "A fulfilled charge line cannot be edited." });
        var unitPrice = decimal.Round(request.UnitPrice ?? line.UnitPrice, 2);
        if (unitPrice != 0 && request.Quantity > MaximumMoney / unitPrice)
            return ValidationProblem("The resulting line exceeds the supported currency precision.");
        var lineTotal = request.Quantity * unitPrice;
        if (lineTotal > MaximumMoney ||
            RecalculateTotal(account) - line.LineTotal > MaximumMoney - decimal.Round(lineTotal, 2))
            return ValidationProblem("The resulting line or account total exceeds the supported currency precision.");
        line.Quantity = request.Quantity;
        if (request.UnitPrice.HasValue)
        {
            line.UnitPrice = decimal.Round(request.UnitPrice.Value, 2);
            line.PriceOverrideReason = request.PriceOverrideReason!.Trim();
        }
        line.LineTotal = decimal.Round(line.Quantity * line.UnitPrice, 2);
        return await SaveDraft(account);
    }

    [HttpDelete("{id}/lines/{lineId}")]
    public async Task<ActionResult<BillingAccountSummary>> DeleteLine(string id, string lineId, [FromQuery] int expectedVersion)
    {
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (account.Version != expectedVersion) return VersionConflict(account);
        if (!CanEdit(account)) return Conflict(new { error = "Only draft accounts can be edited." });
        var line = account.Lines.SingleOrDefault(item => item.ClinicId == ClinicId && item.Id == lineId);
        if (line is null) return NotFound();
        if (line.FulfilledQuantity != 0) return Conflict(new { error = "A fulfilled charge line cannot be deleted." });
        account.Lines.Remove(line);
        db.ChargeLines.Remove(line);
        return await SaveDraft(account);
    }

    [HttpPost("{id}/open")]
    public async Task<ActionResult<BillingAccountSummary>> OpenAccount(string id, AccountVersionRequest request)
    {
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (account.Version != request.ExpectedVersion) return VersionConflict(account);
        if (account.Status != "Draft" || account.Lines.Count == 0)
            return Conflict(new { error = "Only a non-empty draft account can be opened." });
        account.Total = RecalculateTotal(account);
        account.Status = "Open";
        account.OpenedAt = DateTimeOffset.UtcNow;
        account.Version++;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAccount(account)) return VersionConflict(account);
        return Ok(await MapAccount(account));
    }

    [HttpPost("{id}/payments")]
    public async Task<ActionResult<BillingAccountSummary>> AddPayment(string id, PaymentRequest request)
    {
        if (!ValidMoney(request.Amount) || !ValidMethod(request.Method) || !ValidKey(request.IdempotencyKey))
            return ValidationProblem("A positive amount, Cash/Card/Transfer method, and idempotencyKey are required.");
        if (request.ExpectedVersion < 1) return ValidationProblem("expectedVersion must be greater than zero.");
        if (request.Method == "Cash" &&
            (!request.CashReceived.HasValue || request.CashReceived < request.Amount || request.CashReceived > MaximumMoney ||
             request.CashReceived != decimal.Round(request.CashReceived.Value, 2)))
            return ValidationProblem("Cash payments require cashReceived with two-decimal precision covering the payment amount.");
        if (request.Method != "Cash" && request.CashReceived.HasValue)
            return ValidationProblem("cashReceived is only valid for Cash payments.");
        var prior = await db.AccountPayments.AsNoTracking().SingleOrDefaultAsync(payment =>
            payment.ClinicId == ClinicId && payment.IdempotencyKey == request.IdempotencyKey.Trim());
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (prior is not null)
        {
            if (prior.BillingAccountId != id || prior.Kind != "Payment" || prior.Amount != decimal.Round(request.Amount, 2) ||
                prior.Method != request.Method.Trim() || prior.Note != (request.Note?.Trim() ?? string.Empty) ||
                prior.CashReceived != request.CashReceived)
                return Conflict(new { error = "Idempotency key was already used for a different operation." });
            return Ok(await MapAccount(account));
        }
        if (account.Version != request.ExpectedVersion) return VersionConflict(account);
        if (account.Status != "Open") return Conflict(new { error = "Payments can only be recorded on open accounts." });
        var paid = NetPaid(account);
        var amount = decimal.Round(request.Amount, 2);
        if (amount > account.Total - paid) return Conflict(new { error = "Payment exceeds the account balance." });
        account.Payments.Add(new AccountPayment
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, Kind = "Payment",
            Method = request.Method.Trim(), IdempotencyKey = request.IdempotencyKey.Trim(),
            Amount = amount, CashReceived = request.CashReceived,
            ChangeGiven = request.CashReceived.HasValue ? request.CashReceived.Value - amount : null,
            Note = request.Note?.Trim() ?? string.Empty, CreatedAt = DateTimeOffset.UtcNow
        });
        if (paid + amount >= account.Total) { account.Status = "Closed"; account.ClosedAt = DateTimeOffset.UtcNow; }
        account.Version++;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAccount(account)) return VersionConflict(account);
        return Ok(await MapAccount(account));
    }

    [HttpPost("{id}/refunds")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<BillingAccountSummary>> Refund(string id, RefundRequest request)
    {
        if (!ValidMoney(request.Amount) || !ValidMethod(request.Method) || !ValidKey(request.IdempotencyKey))
            return ValidationProblem("A positive amount, Cash/Card/Transfer method, and idempotencyKey are required.");
        if (request.ExpectedVersion < 1) return ValidationProblem("expectedVersion must be greater than zero.");
        var prior = await db.AccountPayments.AsNoTracking().SingleOrDefaultAsync(payment =>
            payment.ClinicId == ClinicId && payment.IdempotencyKey == request.IdempotencyKey.Trim());
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (prior is not null)
        {
            if (prior.BillingAccountId != id || prior.Kind != "Refund" || prior.Amount != decimal.Round(request.Amount, 2) ||
                prior.Method != request.Method.Trim() || prior.Note != (request.Note?.Trim() ?? string.Empty))
                return Conflict(new { error = "Idempotency key was already used for a different operation." });
            return Ok(await MapAccount(account));
        }
        if (account.Version != request.ExpectedVersion) return VersionConflict(account);
        if (account.Status is not ("Closed" or "Voided"))
            return Conflict(new { error = "Refunds can only be recorded for closed or voided accounts." });
        var amount = decimal.Round(request.Amount, 2);
        if (amount > NetPaid(account)) return Conflict(new { error = "Refund exceeds the unrefunded payment amount." });
        account.Payments.Add(new AccountPayment
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, Kind = "Refund",
            Method = request.Method.Trim(), IdempotencyKey = request.IdempotencyKey.Trim(),
            Amount = amount, Note = request.Note?.Trim() ?? string.Empty, CreatedAt = DateTimeOffset.UtcNow
        });
        account.Version++;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAccount(account)) return VersionConflict(account);
        return Ok(await MapAccount(account));
    }

    [HttpPost("{id}/void")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<BillingAccountSummary>> VoidAccount(string id, VoidAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return ValidationProblem("A void reason of at most 500 characters is required.");
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (account.Version != request.ExpectedVersion) return VersionConflict(account);
        if (account.Status is "Closed" or "Voided")
            return Conflict(new { error = "Closed accounts cannot be voided; use a recorded refund where appropriate." });
        account.Status = "Voided";
        account.VoidReason = request.Reason.Trim();
        account.Version++;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAccount(account)) return VersionConflict(account);
        return Ok(await MapAccount(account));
    }

    [HttpPost("{id}/fulfill")]
    public async Task<ActionResult<BillingAccountSummary>> Fulfill(string id, AccountVersionRequest request)
    {
        var account = await LoadAccount(id);
        if (account is null) return NotFound();
        if (account.Version != request.ExpectedVersion) return VersionConflict(account);
        if (account.Status is not ("Draft" or "Open" or "Closed"))
            return Conflict(new { error = "Only draft, open, or closed accounts can be fulfilled." });
        if (!account.Lines.Any(item => item.ItemType == "Product" && item.FulfilledQuantity < item.Quantity))
            return Ok(await MapAccount(account));
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var line in account.Lines.Where(item => item.ItemType == "Product" && item.FulfilledQuantity < item.Quantity))
        {
            var remaining = line.Quantity - line.FulfilledQuantity;
            var product = await db.Products.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == line.CatalogItemId);
            if (product is null) return Conflict(new { error = $"Product for charge line {line.Id} no longer exists." });
            if (product.TrackStock)
            {
                var affected = await db.Products.Where(item => item.ClinicId == ClinicId && item.Id == product.Id &&
                        !item.IsArchived && item.TrackStock && item.StockOnHand >= remaining)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.StockOnHand, item => item.StockOnHand - remaining)
                        .SetProperty(item => item.Version, item => item.Version + 1));
                if (affected != 1) return Conflict(new { error = $"Insufficient stock to fulfill {line.NameSnapshot}." });
                var balance = await db.Products.AsNoTracking().Where(item => item.ClinicId == ClinicId && item.Id == product.Id)
                    .Select(item => item.StockOnHand).SingleAsync();
                db.InventoryMovements.Add(new InventoryMovement
                {
                    Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, ProductId = product.Id,
                    ProductNameSnapshot = line.NameSnapshot, SkuSnapshot = line.SkuSnapshot, Type = "Fulfillment",
                    QuantityChange = -remaining, BalanceAfter = balance, Reason = $"Fulfillment of account {id}",
                    OperationKey = $"{id}:{line.Id}", BillingAccountId = id, ChargeLineId = line.Id, CreatedAt = DateTimeOffset.UtcNow
                });
            }
            line.FulfilledQuantity = line.Quantity;
        }
        account.Version++;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAccount(account)) return VersionConflict(account);
        await transaction.CommitAsync();
        return Ok(await MapAccount(account));
    }

    private async Task<BillingAccount?> LoadAccount(string id) =>
        await db.BillingAccounts.AsSplitQuery().Include(account => account.Lines).Include(account => account.Payments)
            .SingleOrDefaultAsync(account => account.ClinicId == ClinicId && account.Id == id);

    private async Task<ActionResult<BillingAccountSummary>> SaveDraft(BillingAccount account)
    {
        account.Total = RecalculateTotal(account);
        account.Version++;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await SaveAccount(account)) return VersionConflict(account);
        return Ok(await MapAccount(account));
    }

    private async Task<bool> SaveAccount(BillingAccount account)
    {
        try { await db.SaveChangesAsync(); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
        catch (DbUpdateException exception) when (exception.InnerException is not null)
        {
            if (await db.BillingAccounts.AsNoTracking().AnyAsync(item => item.ClinicId == ClinicId && item.Id == account.Id && item.Version != account.Version))
                return false;
            throw;
        }
    }

    private ObjectResult VersionConflict(BillingAccount account) =>
        Conflict(new { error = "Billing account changed; reload before retrying.", currentVersion = account.Version });

    private static bool CanEdit(BillingAccount account) => account.Status == "Draft";
    private static decimal RecalculateTotal(BillingAccount account) => decimal.Round(account.Lines.Sum(line => line.LineTotal), 2);
    private static decimal NetPaid(BillingAccount account) =>
        account.Payments.Sum(payment => payment.Kind == "Refund" ? -payment.Amount : payment.Amount);
    private static bool ValidMoney(decimal amount) => amount > 0 && amount <= 9999999999999999.99m && decimal.Round(amount, 2) == amount;
    private static bool ValidKey(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 100;
    private static bool ValidMethod(string value) => value is "Cash" or "Card" or "Transfer";
    private async Task<BillingAccountSummary> MapAccount(BillingAccount account)
    {
        var patientName = account.PatientId is null ? null :
            await db.Patients.AsNoTracking()
                .Where(patient => patient.ClinicId == ClinicId && patient.Id == account.PatientId)
                .Select(patient => patient.Name).SingleOrDefaultAsync();
        return ToSummary(account, patientName);
    }

    private static BillingAccountSummary ToSummary(BillingAccount account, string? patientName = null)
    {
        var netPaid = NetPaid(account);
        var paymentStatus = netPaid <= 0 ? account.Payments.Any(payment => payment.Kind == "Refund") ? "Refunded" : "Unpaid"
            : account.Payments.Any(payment => payment.Kind == "Refund") ? "PartiallyRefunded"
            : netPaid < account.Total ? "PartiallyPaid" : "Paid";
        return new BillingAccountSummary(account.Id, account.PatientId, patientName, account.ConsultationId,
            account.Currency, account.Status, paymentStatus, account.Total, netPaid, account.Total - netPaid,
            account.Version, account.CreatedAt, account.UpdatedAt, account.OpenedAt, account.ClosedAt, account.VoidReason,
            account.Lines.OrderBy(line => line.Id).Select(line => new AccountLineSummary(line.Id, line.CatalogItemId,
                line.ItemType, line.NameSnapshot, line.SkuSnapshot, line.UnitSnapshot, line.CatalogVersion,
                line.Quantity, line.UnitPrice, line.LineTotal, line.FulfilledQuantity, line.PriceOverrideReason)).ToList(),
            account.Payments.OrderBy(payment => payment.CreatedAt).Select(payment => new AccountPaymentSummary(
                payment.Id, payment.Kind, payment.Method, payment.Amount, payment.Note, payment.CreatedAt,
                payment.CashReceived, payment.ChangeGiven, payment.IdempotencyKey)).ToList());
    }
}
