using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.VAT.Controllers;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
[Route("stores/{storeId}/plugins/vat")]
public class UIVATController : Controller
{
    private readonly IVATCalculationService _vatService;
    private readonly VATRateProvider _rateProvider;
    private readonly VATReportingService _reportingService;

    public UIVATController(
        IVATCalculationService vatService,
        VATRateProvider rateProvider,
        VATReportingService reportingService)
    {
        _vatService = vatService;
        _rateProvider = rateProvider;
        _reportingService = reportingService;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> Settings(string storeId)
    {
        var settings = await _vatService.GetStoreSettingsAsync(storeId);

        var viewModel = new VATSettingsViewModel
        {
            StoreId = storeId,
            Mode = settings?.Mode ?? VATMode.Fixed,
            HomeCountry = settings?.HomeCountry ?? "DE",
            VATNumber = settings?.VATNumber,
            Enabled = settings?.Enabled ?? false,
            ValidateVIES = settings?.ValidateVIES ?? true,
            BelowSmallBusinessThreshold = settings?.BelowSmallBusinessThreshold ?? false,
            EUCountries = _rateProvider.GetAllRates()
                .Select(kvp => new EUCountryOption
                {
                    Code = kvp.Key,
                    Name = kvp.Value.CountryName,
                    StandardRate = kvp.Value.StandardRate
                })
                .OrderBy(c => c.Name)
                .ToList()
        };

        return View(viewModel);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> Settings(string storeId, VATSettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.EUCountries = _rateProvider.GetAllRates()
                .Select(kvp => new EUCountryOption
                {
                    Code = kvp.Key,
                    Name = kvp.Value.CountryName,
                    StandardRate = kvp.Value.StandardRate
                })
                .OrderBy(c => c.Name)
                .ToList();
            return View(model);
        }

        var request = new UpdateVATSettingsRequest
        {
            Mode = model.Mode,
            HomeCountry = model.HomeCountry,
            VATNumber = model.VATNumber,
            Enabled = model.Enabled,
            ValidateVIES = model.ValidateVIES,
            BelowSmallBusinessThreshold = model.BelowSmallBusinessThreshold
        };

        await _vatService.UpdateStoreSettingsAsync(storeId, request);

        TempData["SuccessMessage"] = "VAT settings saved successfully.";
        return RedirectToAction(nameof(Settings), new { storeId });
    }

    [HttpGet("reports")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> Reports(
        string storeId,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        VATReportGrouping grouping = VATReportGrouping.ByCountry)
    {
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, grouping);

        var viewModel = new VATReportsViewModel
        {
            StoreId = storeId,
            StartDate = start,
            EndDate = end,
            Grouping = grouping,
            Report = report
        };

        return View(viewModel);
    }

    [HttpGet("reports/export")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> ExportReport(
        string storeId,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        VATReportGrouping grouping = VATReportGrouping.ByCountry)
    {
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, grouping);
        var csv = _reportingService.ExportToCsv(report);

        var fileName = $"vat-report-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [HttpGet("invoices/{invoiceId}")]
    [Authorize(Policy = Policies.CanViewInvoices)]
    public async Task<IActionResult> InvoiceDetails(string storeId, string invoiceId)
    {
        var record = await _vatService.GetVATRecordAsync(storeId, invoiceId);

        if (record == null)
        {
            return NotFound();
        }

        var viewModel = new VATInvoiceDetailsViewModel
        {
            StoreId = storeId,
            Record = record,
            CountryName = _rateProvider.GetRateInfo(record.CustomerCountry)?.CountryName ?? record.CustomerCountry
        };

        return View(viewModel);
    }
}

public class VATSettingsViewModel
{
    public string StoreId { get; set; } = string.Empty;
    public VATMode Mode { get; set; }
    public string HomeCountry { get; set; } = string.Empty;
    public string? VATNumber { get; set; }
    public bool Enabled { get; set; }
    public bool ValidateVIES { get; set; }
    public bool BelowSmallBusinessThreshold { get; set; }
    public List<EUCountryOption> EUCountries { get; set; } = new();
}

public class EUCountryOption
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal StandardRate { get; set; }
}

public class VATReportsViewModel
{
    public string StoreId { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public VATReportGrouping Grouping { get; set; }
    public VATReport Report { get; set; } = new();
}

public class VATInvoiceDetailsViewModel
{
    public string StoreId { get; set; } = string.Empty;
    public VATInvoiceRecord Record { get; set; } = new();
    public string CountryName { get; set; } = string.Empty;
}
