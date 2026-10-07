using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
[RequireClinicAccess]
public sealed class InventoryController(ClinicDbContext db, CurrentClinic clinic) : ControllerBase
{
    private string ClinicId => clinic.Id;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogItemSummary>>> GetInventory([FromQuery] bool includeArchived = false)
    {
        var products = await db.Products.AsNoTracking()
            .Where(product => product.ClinicId == ClinicId && (includeArchived || !product.IsArchived))
            .OrderBy(product => product.Name)
            .Select(product => new CatalogItemSummary(product.Id, "Product", product.Name, product.Category,
                string.Empty, product.Sku, product.Unit, product.UnitPrice, product.StockOnHand,
                product.LowStockThreshold, product.TrackStock, product.IsFavorite, product.IsArchived, product.Version))
            .ToListAsync();
        return Ok(products);
    }

    [HttpGet("movements")]
    public async Task<ActionResult<IReadOnlyList<InventoryMovementSummary>>> GetMovements(
        [FromQuery] string? productId = null, [FromQuery] int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var movements = await db.InventoryMovements.AsNoTracking()
            .Where(item => item.ClinicId == ClinicId && (productId == null || item.ProductId == productId))
            .OrderByDescending(item => item.CreatedAt.ToString())
            .Take(take)
            .Select(item => new InventoryMovementSummary(item.Id, item.ProductId, item.ProductNameSnapshot,
                item.SkuSnapshot, item.Type, item.QuantityChange, item.BalanceAfter, item.Reason,
                item.BillingAccountId, item.ChargeLineId, item.CreatedAt))
            .ToListAsync();
        return Ok(movements);
    }

    [HttpPost("adjustments")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<IActionResult> Adjust(InventoryAdjustmentRequest request)
    {
        if (request.QuantityChange == 0 || decimal.Abs(request.QuantityChange) > 999999999999999.999m ||
            request.QuantityChange != decimal.Round(request.QuantityChange, 3) ||
            string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500 ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 100)
            return ValidationProblem("A non-zero quantity change, reason of at most 500 characters, and idempotencyKey of at most 100 characters are required.");
        var key = request.IdempotencyKey.Trim();
        var prior = await db.InventoryMovements.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.IdempotencyKey == key);
        if (prior is not null) return ReplayAdjustment(prior, request);
        var product = await db.Products.SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == request.ProductId);
        if (product is null) return NotFound(new { error = "Product not found in this clinic." });
        if (!product.TrackStock) return Conflict(new { error = "Stock tracking is disabled for this product." });
        var balance = product.StockOnHand + request.QuantityChange;
        if (balance < 0) return Conflict(new { error = "Adjustment would result in negative stock." });
        if (balance > 999999999999999.999m)
            return ValidationProblem("The resulting stock exceeds supported inventory precision.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var affected = await db.Products.Where(item => item.ClinicId == ClinicId && item.Id == product.Id &&
                item.TrackStock && item.StockOnHand == product.StockOnHand)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.StockOnHand, item => item.StockOnHand + request.QuantityChange)
                .SetProperty(item => item.Version, item => item.Version + 1));
        if (affected != 1) return Conflict(new { error = "Product stock changed; reload and retry." });

        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, ProductId = product.Id,
            ProductNameSnapshot = product.Name, SkuSnapshot = product.Sku ?? string.Empty,
            Type = request.QuantityChange > 0 ? "Receive" : "Adjustment",
            QuantityChange = request.QuantityChange, BalanceAfter = balance, Reason = request.Reason.Trim(),
            OperationKey = Guid.NewGuid().ToString(), IdempotencyKey = key, CreatedAt = DateTimeOffset.UtcNow
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (CommercialPersistence.IsUniqueViolation(exception))
        {
            await transaction.RollbackAsync();
            prior = await db.InventoryMovements.AsNoTracking()
                .SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.IdempotencyKey == key);
            if (prior is not null) return ReplayAdjustment(prior, request);
            throw;
        }
        await transaction.CommitAsync();
        return Ok(new { productId = product.Id, stockOnHand = balance });
    }

    private IActionResult ReplayAdjustment(InventoryMovement prior, InventoryAdjustmentRequest request)
    {
        if (prior.ProductId != request.ProductId || prior.QuantityChange != request.QuantityChange ||
            prior.Reason != request.Reason.Trim())
            return Conflict(new { error = "Idempotency key was already used for a different inventory adjustment." });
        return Ok(new { productId = prior.ProductId, stockOnHand = prior.BalanceAfter });
    }
}
