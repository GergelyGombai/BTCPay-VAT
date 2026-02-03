using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Models.Response;
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
        var rateInfo = settings != null ? _rateProvider.GetRateInfo(settings.HomeCountry) : null;

        return Ok(new VATSettingsResponse
        {
            StoreId = storeId,
            Mode = settings?.Mode ?? VATMode.Fixed,
            HomeCountry = settings?.HomeCountry ?? "",
            HomeCountryName = rateInfo?.CountryName ?? "",
            VATNumber = settings?.VATNumber,
            Enabled = settings?.Enabled ?? false,
            ValidateVIES = settings?.ValidateVIES ?? true,
            BelowSmallBusinessThreshold = settings?.BelowSmallBusinessThreshold ?? false,
            HomeCountryVATRate = rateInfo?.StandardRate ?? 0
        });
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> Settings(string storeId, [FromBody] UpdateVATSettingsRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (!_rateProvider.IsEUCountry(request.HomeCountry))
        {
            return BadRequest(new { message = $"'{request.HomeCountry}' is not a valid EU country code" });
        }

        var settings = await _vatService.UpdateStoreSettingsAsync(storeId, request);
        var rateInfo = _rateProvider.GetRateInfo(settings.HomeCountry);

        return Ok(new VATSettingsResponse
        {
            StoreId = settings.StoreId,
            Mode = settings.Mode,
            HomeCountry = settings.HomeCountry,
            HomeCountryName = rateInfo?.CountryName ?? settings.HomeCountry,
            VATNumber = settings.VATNumber,
            Enabled = settings.Enabled,
            ValidateVIES = settings.ValidateVIES,
            BelowSmallBusinessThreshold = settings.BelowSmallBusinessThreshold,
            HomeCountryVATRate = rateInfo?.StandardRate ?? 0
        });
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

        return Ok(new
        {
            storeId,
            startDate = start,
            endDate = end,
            grouping = grouping.ToString(),
            summary = new
            {
                totalNetAmount = report.TotalNetAmount,
                totalVATAmount = report.TotalVATAmount,
                totalGrossAmount = report.TotalGrossAmount,
                invoiceCount = report.InvoiceCount,
                reverseChargeCount = report.ReverseChargeCount
            },
            items = report.Items.Select(i => new
            {
                groupKey = i.GroupKey,
                groupLabel = i.GroupLabel,
                countryCode = i.CountryCode,
                netAmount = i.NetAmount,
                vatAmount = i.VATAmount,
                grossAmount = i.GrossAmount,
                invoiceCount = i.InvoiceCount,
                averageVATRate = i.AverageVATRate
            })
        });
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
            return NotFound(new { message = "VAT record not found" });
        }

        return Ok(new
        {
            invoiceId = record.InvoiceId,
            storeId = record.StoreId,
            customerCountry = record.CustomerCountry,
            customerCountryName = _rateProvider.GetRateInfo(record.CustomerCountry)?.CountryName ?? record.CustomerCountry,
            vatRate = record.VATRate,
            netAmount = record.NetAmount,
            vatAmount = record.VATAmount,
            grossAmount = record.GrossAmount,
            currency = record.Currency,
            reverseChargeApplied = record.ReverseChargeApplied,
            customerVATNumber = record.CustomerVATNumber,
            modeApplied = record.ModeApplied.ToString(),
            createdAt = record.CreatedAt
        });
    }
}
