using EasyVetClinic.Api.Authentication;
using EasyVetClinic.Api.Data;
using EasyVetClinic.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EasyVetClinic.Api.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize]
[RequireClinicAccess]
public sealed class OperationsController(ClinicDbContext db, CurrentClinic clinic) : ControllerBase
{
    private string ClinicId => clinic.Id;

    [HttpGet("today")]
    public async Task<ActionResult<TodayOperationsSummary>> Today([FromQuery] string currency = "MXN")
    {
        if (currency.Length != 3 || currency.Any(character => !char.IsAsciiLetter(character)))
            return ValidationProblem("Currency must be a three-letter code.");
        currency = currency.ToUpperInvariant();
        var zone = await GetTimeZone();
        var today = LocalDate(DateTimeOffset.UtcNow, zone);
        var consultations = await (
            from consultation in db.Consultations.AsNoTracking()
            join patient in db.Patients.AsNoTracking() on consultation.PatientId equals patient.Id
            join guardian in db.Guardians.AsNoTracking() on patient.GuardianId equals guardian.Id
            where consultation.ClinicId == ClinicId && patient.ClinicId == ClinicId &&
                guardian.ClinicId == ClinicId && consultation.Status == "InProgress"
            select new { consultation.Id, consultation.PatientId, PatientName = patient.Name,
                GuardianName = guardian.Name, consultation.ClinicianName, consultation.StartedAt,
                consultation.Status, consultation.Version }
        ).ToListAsync();
        var accounts = await db.BillingAccounts.AsNoTracking()
            .Where(account => account.ClinicId == ClinicId)
            .Select(account => new { account.Id, account.ConsultationId, account.Currency, account.Status, account.Total })
            .ToListAsync();
        var payments = await db.AccountPayments.AsNoTracking().Where(payment => payment.ClinicId == ClinicId)
            .Select(payment => new { payment.BillingAccountId, payment.Kind, payment.Amount }).ToListAsync();
        var netPaid = payments.GroupBy(payment => payment.BillingAccountId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Kind == "Refund" ? -payment.Amount : payment.Amount));
        var pending = accounts.Where(account => account.Status is "Draft" or "Open").ToList();
        var balances = pending.GroupBy(account => account.Currency)
            .Select(group => new OutstandingCurrencySummary(group.Key,
                group.Sum(account => Math.Max(0, account.Total - netPaid.GetValueOrDefault(account.Id)))))
            .OrderBy(item => item.Currency).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(product => product.ClinicId == ClinicId && product.TrackStock && !product.IsArchived)
            .Select(product => new { product.StockOnHand, product.LowStockThreshold }).ToListAsync();
        var consultationAccounts = accounts.Where(account => account.ConsultationId is not null)
            .ToDictionary(account => account.ConsultationId!, account => account.Id);
        return Ok(new TodayOperationsSummary(today, zone.Id, currency, consultations.Count, pending.Count,
            balances.SingleOrDefault(item => item.Currency == currency)?.OutstandingBalance ?? 0,
            products.Count(product => product.StockOnHand <= product.LowStockThreshold), balances,
            consultations.OrderBy(item => item.StartedAt).Select(item => new ActiveConsultationSummary(
                item.Id, item.PatientId, item.PatientName, item.GuardianName, item.ClinicianName, item.StartedAt,
                item.Status, item.Version, consultationAccounts.GetValueOrDefault(item.Id))).ToList()));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<OperationsRangeSummary>> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var zone = await GetTimeZone();
        var start = from ?? LocalDate(DateTimeOffset.UtcNow, zone);
        var end = to ?? start;
        if (end < start || end.DayNumber - start.DayNumber > 366)
            return ValidationProblem("The inclusive date range must be ordered and span at most 367 days.");
        var accounts = await db.BillingAccounts.AsNoTracking().Where(account => account.ClinicId == ClinicId)
            .Select(account => new { account.Id, account.Currency, account.Status, account.Total, account.OpenedAt })
            .ToListAsync();
        var accountCurrencies = accounts.ToDictionary(account => account.Id, account => account.Currency);
        var payments = await db.AccountPayments.AsNoTracking().Where(payment => payment.ClinicId == ClinicId)
            .Select(payment => new { payment.BillingAccountId, payment.Amount, payment.Kind, payment.CreatedAt })
            .ToListAsync();
        var rangedAccounts = accounts.Where(account => account.Status != "Voided" && account.OpenedAt.HasValue &&
            InRange(account.OpenedAt.Value, start, end, zone)).ToList();
        var rangedPayments = payments.Where(payment => accountCurrencies.ContainsKey(payment.BillingAccountId) &&
            InRange(payment.CreatedAt, start, end, zone)).ToList();
        var currencies = rangedAccounts.Select(account => account.Currency)
            .Concat(rangedPayments.Select(payment => accountCurrencies[payment.BillingAccountId]))
            .Distinct().OrderBy(value => value);
        var totals = currencies.Select(currency =>
        {
            var collected = rangedPayments.Where(payment => payment.Kind == "Payment" &&
                accountCurrencies[payment.BillingAccountId] == currency).Sum(payment => payment.Amount);
            var refunded = rangedPayments.Where(payment => payment.Kind == "Refund" &&
                accountCurrencies[payment.BillingAccountId] == currency).Sum(payment => payment.Amount);
            return new CurrencyOperationsSummary(currency,
                rangedAccounts.Where(account => account.Currency == currency).Sum(account => account.Total),
                collected, refunded, collected - refunded);
        }).ToList();
        return Ok(new OperationsRangeSummary(start, end, zone.Id, rangedAccounts.Count,
            rangedPayments.Count(payment => payment.Kind == "Payment"),
            rangedPayments.Count(payment => payment.Kind == "Refund"), totals));
    }

    private async Task<TimeZoneInfo> GetTimeZone()
    {
        var timeZoneId = await db.Clinics.AsNoTracking().Where(item => item.Id == ClinicId)
            .Select(item => item.TimeZoneId).SingleAsync();
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    private static DateOnly LocalDate(DateTimeOffset value, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime);

    private static bool InRange(DateTimeOffset value, DateOnly start, DateOnly end, TimeZoneInfo zone)
    {
        var date = LocalDate(value, zone);
        return date >= start && date <= end;
    }
}
