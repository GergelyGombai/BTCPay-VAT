using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.VAT.Controllers;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie, Policy = Policies.CanModifyStoreSettings)]
[Route("stores/{storeId}/vat")]
public class UIStoreVATController : Controller
{
    private readonly IVATCalculationService _vatService;
    private readonly VATRateProvider _rateProvider;
    private readonly VATReportingService _reportingService;
    private readonly UIInvoiceController _invoiceController;

    public UIStoreVATController(
        IVATCalculationService vatService,
        VATRateProvider rateProvider,
        VATReportingService reportingService,
        UIInvoiceController invoiceController)
    {
        _vatService = vatService;
        _rateProvider = rateProvider;
        _reportingService = reportingService;
        _invoiceController = invoiceController;
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
            EUCountries = GetEUCountryOptions()
        };

        return View("/Plugins/VAT/Views/UIStoreVAT/Index.cshtml", viewModel);
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
            model.EUCountries = GetEUCountryOptions();
            return View("/Plugins/VAT/Views/UIStoreVAT/Index.cshtml", model);
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

    [HttpGet("create-invoice")]
    [Authorize(Policy = Policies.CanCreateInvoice)]
    public async Task<IActionResult> CreateInvoice(string storeId)
    {
        var settings = await _vatService.GetStoreSettingsAsync(storeId);

        var viewModel = new CreateVATInvoiceViewModel
        {
            StoreId = storeId,
            Currency = "EUR",
            CustomerCountry = settings?.HomeCountry ?? "DE",
            VATEnabled = settings?.Enabled ?? false,
            VATMode = settings?.Mode ?? VATMode.Fixed,
            EUCountries = GetEUCountryOptions()
        };

        return View("/Plugins/VAT/Views/UIStoreVAT/CreateInvoice.cshtml", viewModel);
    }

    [HttpPost("create-invoice")]
    [Authorize(Policy = Policies.CanCreateInvoice)]
    public async Task<IActionResult> CreateInvoice(string storeId, CreateVATInvoiceViewModel model, CancellationToken cancellationToken)
    {
        var settings = await _vatService.GetStoreSettingsAsync(storeId, cancellationToken);

        if (settings == null || !settings.Enabled)
        {
            ModelState.AddModelError("", "VAT is not enabled for this store. Configure it first.");
        }

        if (model.Amount <= 0)
        {
            ModelState.AddModelError(nameof(model.Amount), "Amount must be greater than 0");
        }

        if (string.IsNullOrWhiteSpace(model.CustomerCountry) || model.CustomerCountry.Length != 2)
        {
            ModelState.AddModelError(nameof(model.CustomerCountry), "A valid 2-letter country code is required");
        }

        if (!ModelState.IsValid)
        {
            model.StoreId = storeId;
            model.VATEnabled = settings?.Enabled ?? false;
            model.VATMode = settings?.Mode ?? VATMode.Fixed;
            model.EUCountries = GetEUCountryOptions();
            return View("/Plugins/VAT/Views/UIStoreVAT/CreateInvoice.cshtml", model);
        }

        // Calculate VAT
        var vatResult = await _vatService.CalculateVATAsync(
            storeId,
            model.Amount,
            model.Currency,
            model.CustomerCountry,
            model.CustomerVATNumber,
            cancellationToken);

        // Get the store
        var store = HttpContext.GetStoreData();
        if (store == null)
        {
            return NotFound();
        }

        // Build metadata
        var metadata = new JObject
        {
            ["vatData"] = JObject.FromObject(new
            {
                netAmount = vatResult.NetAmount,
                vatRate = vatResult.VATRate,
                vatAmount = vatResult.VATAmount,
                grossAmount = vatResult.GrossAmount,
                customerCountry = vatResult.CountryCode,
                reverseCharge = vatResult.ReverseChargeApplied,
                customerVATNumber = vatResult.CustomerVATNumber,
                modeApplied = vatResult.ModeApplied.ToString()
            }),
            ["buyerCountry"] = model.CustomerCountry
        };

        if (!string.IsNullOrEmpty(model.BuyerEmail))
            metadata["buyerEmail"] = model.BuyerEmail;
        if (!string.IsNullOrEmpty(model.BuyerName))
            metadata["buyerName"] = model.BuyerName;
        if (!string.IsNullOrEmpty(model.OrderId))
            metadata["orderId"] = model.OrderId;
        if (!string.IsNullOrEmpty(model.ItemDesc))
            metadata["itemDesc"] = model.ItemDesc;

        // Create BTCPay invoice
        try
        {
            var invoice = await _invoiceController.CreateInvoiceCoreRaw(
                new CreateInvoiceRequest
                {
                    Amount = vatResult.GrossAmount,
                    Currency = model.Currency,
                    Metadata = metadata
                },
                store,
                Request.GetAbsoluteRoot(),
                cancellationToken: cancellationToken);

            // Save VAT record
            await _vatService.SaveVATRecordAsync(
                invoice.Id,
                storeId,
                vatResult,
                model.CustomerCountry,
                model.CustomerVATNumber,
                cancellationToken);

            TempData[WellKnownTempData.SuccessMessage] =
                $"Invoice created: {vatResult.NetAmount:N2} {model.Currency} + {vatResult.VATAmount:N2} VAT ({vatResult.VATRate}%) = {vatResult.GrossAmount:N2} {model.Currency}";

            return RedirectToAction("CreateInvoice", new { storeId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Failed to create invoice: {ex.Message}");
            model.StoreId = storeId;
            model.VATEnabled = settings?.Enabled ?? false;
            model.VATMode = settings?.Mode ?? VATMode.Fixed;
            model.EUCountries = GetEUCountryOptions();
            return View("/Plugins/VAT/Views/UIStoreVAT/CreateInvoice.cshtml", model);
        }
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

        return View("/Plugins/VAT/Views/UIStoreVAT/Reports.cshtml", viewModel);
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

    private List<EUCountryOption> GetEUCountryOptions()
    {
        return _rateProvider.GetAllRates()
            .Select(kvp => new EUCountryOption
            {
                Code = kvp.Key,
                Name = kvp.Value.CountryName,
                StandardRate = kvp.Value.StandardRate
            })
            .OrderBy(c => c.Name)
            .ToList();
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

public class CreateVATInvoiceViewModel
{
    public string StoreId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string CustomerCountry { get; set; } = string.Empty;
    public string? CustomerVATNumber { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerName { get; set; }
    public string? OrderId { get; set; }
    public string? ItemDesc { get; set; }

    // Display info
    public bool VATEnabled { get; set; }
    public VATMode VATMode { get; set; }
    public List<EUCountryOption> EUCountries { get; set; } = new();
}

public class VATReportsViewModel
{
    public string StoreId { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public VATReport Report { get; set; } = new();
}
