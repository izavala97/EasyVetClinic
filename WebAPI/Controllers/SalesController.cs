using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
[RequireClinicAccess]
public sealed class SalesController(ClinicDbContext dbContext, CurrentClinic currentClinic) : ControllerBase
{
    private string ClinicId => currentClinic.Id;

    [HttpGet("products")]
    public async Task<ActionResult<IReadOnlyList<ProductSummary>>> GetProducts()
    {
        var products = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.ClinicId == ClinicId)
            .OrderBy(product => product.Category)
            .ThenBy(product => product.Name)
            .Select(product => new ProductSummary(product.Id, product.Name, product.Category, product.UnitPrice, product.StockOnHand))
            .ToListAsync();
        return Ok(products);
    }

    [HttpPost("sales")]
    public async Task<ActionResult<SaleReceipt>> CompleteSale(CheckoutRequest request)
    {
        if ((request.Lines.Count == 0 && request.ServiceLines.Count == 0) || string.IsNullOrWhiteSpace(request.PaymentMethod) || request.Lines.Any(line => line.Quantity <= 0) || request.ServiceLines.Any(line => string.IsNullOrWhiteSpace(line.Name) || line.UnitPrice <= 0))
        {
            return ValidationProblem("A payment method and at least one valid product or service line are required.");
        }

        if (!string.IsNullOrWhiteSpace(request.PatientId) && !await dbContext.Patients.AnyAsync(patient => patient.ClinicId == ClinicId && patient.Id == request.PatientId))
        {
            return NotFound();
        }

        var requestedQuantities = request.Lines
            .GroupBy(line => line.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));
        var products = await dbContext.Products
            .Where(product => product.ClinicId == ClinicId && requestedQuantities.Keys.Contains(product.Id))
            .ToListAsync();

        if (products.Count != requestedQuantities.Count || products.Any(product => product.StockOnHand < requestedQuantities[product.Id]))
        {
            return Conflict("One or more products are unavailable in the requested quantity.");
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid().ToString(),
            ClinicId = ClinicId,
            PatientId = request.PatientId,
            CompletedAt = DateTimeOffset.UtcNow,
            PaymentMethod = request.PaymentMethod.Trim()
        };

        foreach (var product in products)
        {
            var quantity = requestedQuantities[product.Id];
            product.StockOnHand -= quantity;
            sale.Lines.Add(new SaleLine
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.UnitPrice,
                Quantity = quantity
            });
        }

        foreach (var service in request.ServiceLines)
        {
            sale.Lines.Add(new SaleLine
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = string.Empty,
                ProductName = service.Name.Trim(),
                UnitPrice = service.UnitPrice,
                Quantity = 1
            });
        }

        sale.Total = sale.Lines.Sum(line => line.UnitPrice * line.Quantity);
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        return Created($"api/sales/{sale.Id}", new SaleReceipt(
            sale.Id,
            sale.CompletedAt,
            sale.Total,
            sale.PaymentMethod,
            sale.Lines.Select(line => new SaleReceiptLine(line.ProductName, line.Quantity, line.UnitPrice)).ToList()));
    }
}
