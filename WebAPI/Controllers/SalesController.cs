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
    public IActionResult CompleteSale() =>
        StatusCode(StatusCodes.Status410Gone, new ProblemDetails
        {
            Status = StatusCodes.Status410Gone,
            Title = "Legacy checkout is no longer available.",
            Detail = "Create an account through /api/billing/accounts, persist its catalog charge lines, open it, and record payments and fulfillment separately. This endpoint never changes stock."
        });
}
