using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Models.Response;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.VAT.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
[EnableCors(CorsPolicies.All)]
public class GreenFieldVATController : ControllerBase
{
    private readonly IVATCalculationService _vatService;
    private readonly VATRateProvider _rateProvider;
    private readonly VIESValidationService _viesService;
    private readonly UIInvoiceController _invoiceController;

    public GreenFieldVATController(
        IVATCalculationService vatService,
        VATRateProvider rateProvider,
        VIESValidationService viesService,
        UIInvoiceController invoiceController)
    {
        _vatService = vatService;
        _rateProvider = rateProvider;
        _viesService = viesService;
        _invoiceController = invoiceController;
    }

    /// <summary>
    /// Get VAT settings for a store
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/vat/settings")]
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<ActionResult<VATSettingsResponse>> GetSettings(string storeId, CancellationToken cancellationToken)
    {
        var settings = await _vatService.GetStoreSettingsAsync(storeId, cancellationToken);

        if (settings == null)
        {
            return NotFound(new { message = "VAT settings not configured for this store" });
        }

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
            HomeCountryVATRate = rateInfo?.StandardRate ?? 0
        });
    }

    /// <summary>
    /// Update VAT settings for a store
    /// </summary>
    [HttpPut("~/api/v1/stores/{storeId}/vat/settings")]
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<ActionResult<VATSettingsResponse>> UpdateSettings(
        string storeId,
        [FromBody] UpdateVATSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Validate home country is an EU country
        if (!_rateProvider.IsEUCountry(request.HomeCountry))
        {
            return BadRequest(new { message = $"'{request.HomeCountry}' is not a valid EU country code" });
        }

        var settings = await _vatService.UpdateStoreSettingsAsync(storeId, request, cancellationToken);
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
            HomeCountryVATRate = rateInfo?.StandardRate ?? 0
        });
    }

    /// <summary>
    /// Calculate VAT without creating an invoice (preview)
    /// </summary>
    [HttpPost("~/api/v1/stores/{storeId}/vat/calculate")]
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<ActionResult<VATCalculationResult>> CalculateVAT(
        string storeId,
        [FromBody] CalculateVATRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _vatService.CalculateVATAsync(
            storeId,
            request.Amount,
            request.Currency,
            request.CustomerCountry,
            request.CustomerVATNumber,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Create an invoice with VAT calculation
    /// </summary>
    [HttpPost("~/api/v1/stores/{storeId}/vat/invoices")]
    [Authorize(Policy = Policies.CanCreateInvoice, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<ActionResult<VATInvoiceResult>> CreateInvoiceWithVAT(
        string storeId,
        [FromBody] CreateVATInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Get store data
        var store = HttpContext.GetStoreData();
        if (store == null)
        {
            return NotFound(new { message = "Store not found" });
        }

        // Calculate VAT
        var vatResult = await _vatService.CalculateVATAsync(
            storeId,
            request.Amount,
            request.Currency,
            request.CustomerCountry,
            request.CustomerVATNumber,
            cancellationToken);

        // Build metadata with VAT information
        var metadata = request.Metadata != null
            ? JObject.FromObject(request.Metadata)
            : new JObject();

        metadata["vatData"] = JObject.FromObject(new
        {
            netAmount = vatResult.NetAmount,
            vatRate = vatResult.VATRate,
            vatAmount = vatResult.VATAmount,
            grossAmount = vatResult.GrossAmount,
            customerCountry = vatResult.CountryCode,
            reverseCharge = vatResult.ReverseChargeApplied,
            customerVATNumber = vatResult.CustomerVATNumber,
            modeApplied = vatResult.ModeApplied.ToString()
        });

        // Set buyer country for BTCPay's built-in reporting
        metadata["buyerCountry"] = request.CustomerCountry;

        // Create the BTCPay invoice request
        var invoiceRequest = new CreateInvoiceRequest
        {
            Amount = vatResult.GrossAmount,
            Currency = request.Currency,
            Metadata = metadata,
            Checkout = new CreateInvoiceRequest.CheckoutOptions
            {
                Expiration = request.Checkout?.ExpirationMinutes != null
                    ? TimeSpan.FromMinutes(request.Checkout.ExpirationMinutes.Value)
                    : null,
                PaymentMethods = request.Checkout?.PaymentMethods
            },
            Receipt = new InvoiceDataBase.ReceiptOptions
            {
                Enabled = true
            }
        };

        // Add buyer info if provided
        if (!string.IsNullOrEmpty(request.BuyerEmail))
        {
            metadata["buyerEmail"] = request.BuyerEmail;
        }
        if (!string.IsNullOrEmpty(request.BuyerName))
        {
            metadata["buyerName"] = request.BuyerName;
        }
        if (!string.IsNullOrEmpty(request.OrderId))
        {
            metadata["orderId"] = request.OrderId;
        }

        // Create the invoice via BTCPay's invoice controller
        try
        {
            var invoice = await _invoiceController.CreateInvoiceCoreRaw(
                invoiceRequest,
                store,
                Request.GetAbsoluteRoot(),
                additionalTags: null,
                cancellationToken);

            // Save VAT record with real invoice ID
            await _vatService.SaveVATRecordAsync(
                invoice.Id,
                storeId,
                vatResult,
                request.CustomerCountry,
                request.CustomerVATNumber,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetInvoiceVATDetails),
                new { storeId, invoiceId = invoice.Id },
                new VATInvoiceResult
                {
                    InvoiceId = invoice.Id,
                    CheckoutUrl = $"/i/{invoice.Id}",
                    Status = invoice.Status.ToString(),
                    VAT = vatResult,
                    CreatedAt = invoice.InvoiceTime,
                    ExpiresAt = invoice.ExpirationTime
                });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Failed to create invoice: {ex.Message}" });
        }
    }

    /// <summary>
    /// Get VAT details for an existing invoice
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/vat/invoices/{invoiceId}")]
    [Authorize(Policy = Policies.CanViewInvoices, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<ActionResult<VATInvoiceDetailsResponse>> GetInvoiceVATDetails(
        string storeId,
        string invoiceId,
        CancellationToken cancellationToken)
    {
        var record = await _vatService.GetVATRecordAsync(storeId, invoiceId, cancellationToken);

        if (record == null)
        {
            return NotFound(new { message = "VAT record not found for this invoice" });
        }

        return Ok(new VATInvoiceDetailsResponse
        {
            InvoiceId = record.InvoiceId,
            StoreId = record.StoreId,
            CustomerCountry = record.CustomerCountry,
            VATRate = record.VATRate,
            NetAmount = record.NetAmount,
            VATAmount = record.VATAmount,
            GrossAmount = record.GrossAmount,
            Currency = record.Currency,
            ReverseChargeApplied = record.ReverseChargeApplied,
            CustomerVATNumber = record.CustomerVATNumber,
            ModeApplied = record.ModeApplied,
            CreatedAt = record.CreatedAt,
            Evidence = record.Evidence.Select(e => new VATEvidenceResponse
            {
                Type = e.Type.ToString(),
                CountryCode = e.CountryCode,
                Confidence = e.Confidence,
                CollectedAt = e.CollectedAt
            }).ToList()
        });
    }

    /// <summary>
    /// Get all EU VAT rates
    /// </summary>
    [HttpGet("~/api/v1/vat/rates")]
    [AllowAnonymous]
    public ActionResult<VATRatesResponse> GetAllRates()
    {
        var rates = _rateProvider.GetAllRates();

        return Ok(new VATRatesResponse
        {
            Rates = rates.Select(kvp => new CountryVATRate
            {
                CountryCode = kvp.Key,
                CountryName = kvp.Value.CountryName,
                StandardRate = kvp.Value.StandardRate,
                ReducedRate = kvp.Value.ReducedRate,
                SuperReducedRate = kvp.Value.SuperReducedRate
            }).OrderBy(r => r.CountryName).ToList()
        });
    }

    /// <summary>
    /// Validate a VAT number via VIES
    /// </summary>
    [HttpGet("~/api/v1/vat/validate/{vatNumber}")]
    [AllowAnonymous]
    public async Task<ActionResult<VIESValidationResponse>> ValidateVATNumber(
        string vatNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(vatNumber))
        {
            return BadRequest(new { message = "VAT number is required" });
        }

        var result = await _viesService.ValidateVATNumberAsync(vatNumber, cancellationToken);

        return Ok(result);
    }
}

public class VATInvoiceDetailsResponse
{
    public string InvoiceId { get; set; } = string.Empty;
    public string StoreId { get; set; } = string.Empty;
    public string CustomerCountry { get; set; } = string.Empty;
    public decimal VATRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool ReverseChargeApplied { get; set; }
    public string? CustomerVATNumber { get; set; }
    public Data.Models.VATMode ModeApplied { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<VATEvidenceResponse> Evidence { get; set; } = new();
}

public class VATEvidenceResponse
{
    public string Type { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public DateTimeOffset CollectedAt { get; set; }
}
