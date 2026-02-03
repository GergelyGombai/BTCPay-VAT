using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.VAT.Controllers;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
[Route("stores/{storeId}/vat")]
public class UIStoreVATController : Controller
{
    private readonly IVATCalculationService _vatService;
    private readonly VATRateProvider _rateProvider;
    private readonly VATReportingService _reportingService;

    public UIStoreVATController(
        IVATCalculationService vatService,
        VATRateProvider rateProvider,
        VATReportingService reportingService)
    {
        _vatService = vatService;
        _rateProvider = rateProvider;
        _reportingService = reportingService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string storeId)
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

    [HttpPost("")]
    public async Task<IActionResult> Index(string storeId, VATSettingsViewModel model)
    {
        if (!_rateProvider.IsEUCountry(model.HomeCountry))
        {
            ModelState.AddModelError(nameof(model.HomeCountry), "Invalid EU country code");
        }

        if (!ModelState.IsValid)
        {
            model.StoreId = storeId;
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

        TempData[WellKnownTempData.SuccessMessage] = "VAT settings updated successfully";
        return RedirectToAction(nameof(Index), new { storeId });
    }

    [HttpGet("reports")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> Reports(string storeId, DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, VATReportGrouping.ByCountry);

        var viewModel = new VATReportsViewModel
        {
            StoreId = storeId,
            StartDate = start,
            EndDate = end,
            Report = report
        };

        return View(viewModel);
    }

    [HttpGet("reports/export")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> ExportReport(string storeId, DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, VATReportGrouping.ByCountry);
        var csv = _reportingService.ExportToCsv(report);

        var fileName = $"vat-report-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
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
    public VATReport Report { get; set; } = new();
}
