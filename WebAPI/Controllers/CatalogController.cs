using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api/catalog")]
[Authorize]
[RequireClinicAccess]
public sealed class CatalogController(ClinicDbContext db, CurrentClinic clinic) : ControllerBase
{
    private string ClinicId => clinic.Id;

    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyList<CatalogItemSummary>>> GetProducts([FromQuery] bool includeArchived = false)
    {
        var products = await db.Products.AsNoTracking()
            .Where(item => item.ClinicId == ClinicId && (includeArchived || !item.IsArchived))
            .OrderBy(item => item.Name)
            .ToListAsync();
        return Ok(products.Select(ToSummary));
    }

    [HttpGet("products/{id}")]
    public async Task<ActionResult<CatalogItemSummary>> GetProduct(string id)
    {
        var product = await db.Products.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == id);
        return product is null ? NotFound() : Ok(ToSummary(product));
    }

    [HttpPost("products")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<CatalogItemSummary>> CreateProduct(CatalogItemRequest request)
    {
        if (!Valid(request) || (!request.TrackStock && request.StockOnHand != 0))
            return ValidationProblem("Product name, unit, and valid price, stock, and tracking values are required.");
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Sku = NormalizeSku(request.Sku),
            Unit = request.Unit.Trim(),
            UnitPrice = decimal.Round(request.UnitPrice, 2),
            StockOnHand = request.StockOnHand,
            LowStockThreshold = request.LowStockThreshold,
            TrackStock = request.TrackStock,
            IsFavorite = request.IsFavorite
        };
        if (await ProductNameExists(product.Name)) return Conflict(new { error = "A catalog item with this name already exists." });
        if (await SkuExists(product.Sku)) return Conflict(new { error = "A product with this SKU already exists." });
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
            if (product.TrackStock && product.StockOnHand != 0)
                AddMovement(product, "Initial", product.StockOnHand, "Initial stock", Guid.NewGuid().ToString());
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        return CreatedAtAction(nameof(GetProducts), new { id = product.Id }, ToSummary(product));
    }

    [HttpPut("products/{id}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<CatalogItemSummary>> UpdateProduct(string id, CatalogItemRequest request, [FromQuery] int expectedVersion)
    {
        if (!Valid(request) || expectedVersion < 1) return ValidationProblem("A valid product and expectedVersion are required.");
        var product = await db.Products.SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == id);
        if (product is null) return NotFound();
        if (product.Version != expectedVersion) return Conflict(new { error = "Catalog item changed; reload before updating.", currentVersion = product.Version });
        if (product.TrackStock && !request.TrackStock && product.StockOnHand != 0)
            return Conflict(new { error = "Reduce stock to zero with an inventory adjustment before disabling stock tracking." });
        if (await ProductNameExists(request.Name.Trim(), id)) return Conflict(new { error = "A catalog item with this name already exists." });
        var sku = NormalizeSku(request.Sku);
        if (await SkuExists(sku, id)) return Conflict(new { error = "A product with this SKU already exists." });
        product.Name = request.Name.Trim();
        product.Category = request.Category.Trim();
        product.Sku = sku;
        product.Unit = request.Unit.Trim();
        product.UnitPrice = decimal.Round(request.UnitPrice, 2);
        product.LowStockThreshold = request.LowStockThreshold;
        product.TrackStock = request.TrackStock;
        product.IsFavorite = request.IsFavorite;
        product.Version++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Catalog item changed; reload before updating." });
        }
        return Ok(ToSummary(product));
    }

    [HttpDelete("products/{id}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<IActionResult> ArchiveProduct(string id)
    {
        var product = await db.Products.SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == id);
        if (product is null) return NotFound();
        product.IsArchived = true;
        product.Version++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Catalog item changed; reload before archiving." });
        }
        return NoContent();
    }

    [HttpGet("services")]
    public async Task<ActionResult<IReadOnlyList<CatalogItemSummary>>> GetServices([FromQuery] bool includeArchived = false)
    {
        var services = await db.CatalogServices.AsNoTracking()
            .Where(item => item.ClinicId == ClinicId && (includeArchived || !item.IsArchived))
            .OrderBy(item => item.Name)
            .ToListAsync();
        return Ok(services.Select(ToSummary));
    }

    [HttpGet("services/{id}")]
    public async Task<ActionResult<CatalogItemSummary>> GetService(string id)
    {
        var service = await db.CatalogServices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == id);
        return service is null ? NotFound() : Ok(ToSummary(service));
    }

    [HttpPost("services")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<CatalogItemSummary>> CreateService(CatalogItemRequest request)
    {
        if (!Valid(request, checkStock: false)) return ValidationProblem("Service name, unit, and non-negative price are required.");
        var service = new CatalogService
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Unit = request.Unit.Trim(),
            UnitPrice = decimal.Round(request.UnitPrice, 2),
            IsFavorite = request.IsFavorite
        };
        if (await db.CatalogServices.AnyAsync(item => item.ClinicId == ClinicId && item.Name == service.Name))
            return Conflict(new { error = "A service with this name already exists." });
        db.CatalogServices.Add(service);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetServices), new { id = service.Id }, ToSummary(service));
    }

    [HttpPut("services/{id}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<ActionResult<CatalogItemSummary>> UpdateService(string id, CatalogItemRequest request, [FromQuery] int expectedVersion)
    {
        if (!Valid(request, checkStock: false) || expectedVersion < 1) return ValidationProblem("A valid service and expectedVersion are required.");
        var service = await db.CatalogServices.SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == id);
        if (service is null) return NotFound();
        if (service.Version != expectedVersion) return Conflict(new { error = "Catalog item changed; reload before updating.", currentVersion = service.Version });
        if (await db.CatalogServices.AnyAsync(item => item.ClinicId == ClinicId && item.Id != id && item.Name == request.Name.Trim()))
            return Conflict(new { error = "A service with this name already exists." });
        service.Name = request.Name.Trim();
        service.Category = request.Category.Trim();
        service.Description = request.Description?.Trim() ?? string.Empty;
        service.Unit = request.Unit.Trim();
        service.UnitPrice = decimal.Round(request.UnitPrice, 2);
        service.IsFavorite = request.IsFavorite;
        service.Version++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Catalog item changed; reload before updating." });
        }
        return Ok(ToSummary(service));
    }

    [HttpDelete("services/{id}")]
    [RequireClinicRole(ClinicRoles.ClinicAdmin)]
    public async Task<IActionResult> ArchiveService(string id)
    {
        var service = await db.CatalogServices.SingleOrDefaultAsync(item => item.ClinicId == ClinicId && item.Id == id);
        if (service is null) return NotFound();
        service.IsArchived = true;
        service.Version++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Catalog item changed; reload before archiving." });
        }
        return NoContent();
    }

    private async Task<bool> ProductNameExists(string name, string? excludeId = null) =>
        await db.Products.AnyAsync(item => item.ClinicId == ClinicId && item.Name == name && item.Id != excludeId);

    private async Task<bool> SkuExists(string? sku, string? excludeId = null) =>
        sku is not null && await db.Products.AnyAsync(item => item.ClinicId == ClinicId && item.Sku == sku && item.Id != excludeId);

    private void AddMovement(Product product, string type, decimal change, string reason, string operationKey)
    {
        db.InventoryMovements.Add(new InventoryMovement
        {
            Id = Guid.NewGuid().ToString(), ClinicId = ClinicId, ProductId = product.Id,
            ProductNameSnapshot = product.Name, SkuSnapshot = product.Sku ?? string.Empty, Type = type,
            QuantityChange = change, BalanceAfter = product.StockOnHand, Reason = reason,
            OperationKey = operationKey, CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string? NormalizeSku(string? sku) => string.IsNullOrWhiteSpace(sku) ? null : sku.Trim();
    private static bool Valid(CatalogItemRequest item, bool checkStock = true) =>
        !string.IsNullOrWhiteSpace(item.Name) && item.Name.Length <= 200 &&
        !string.IsNullOrWhiteSpace(item.Category) && item.Category.Length <= 100 &&
        !string.IsNullOrWhiteSpace(item.Unit) && item.Unit.Length <= 50 &&
        (item.Description?.Length ?? 0) <= 1000 &&
        item.UnitPrice >= 0 && item.UnitPrice <= 9999999999999999.99m &&
        item.UnitPrice == decimal.Round(item.UnitPrice, 2) &&
        item.LowStockThreshold >= 0 && item.LowStockThreshold <= 999999999999999.999m &&
        item.LowStockThreshold == decimal.Round(item.LowStockThreshold, 3) &&
        (!checkStock || item.StockOnHand >= 0 && item.StockOnHand <= 999999999999999.999m &&
            item.StockOnHand == decimal.Round(item.StockOnHand, 3)) &&
        (item.Sku?.Length ?? 0) <= 100;
    private static CatalogItemSummary ToSummary(Product item) =>
        new(item.Id, "Product", item.Name, item.Category, string.Empty, item.Sku, item.Unit, item.UnitPrice,
            item.StockOnHand, item.LowStockThreshold, item.TrackStock, item.IsFavorite, item.IsArchived, item.Version);
    private static CatalogItemSummary ToSummary(CatalogService item) =>
        new(item.Id, "Service", item.Name, item.Category, item.Description, null, item.Unit, item.UnitPrice,
            0, 0, false, item.IsFavorite, item.IsArchived, item.Version);
}
