using BTCPayServer.Plugins.VAT.Data;
using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Models.Response;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.VAT.Services;

public class VATCalculationService : IVATCalculationService
{
    private readonly VATDbContext _dbContext;
    private readonly VATRateProvider _rateProvider;
    private readonly VIESValidationService _viesService;

    public VATCalculationService(
        VATDbContext dbContext,
        VATRateProvider rateProvider,
        VIESValidationService viesService)
    {
        _dbContext = dbContext;
        _rateProvider = rateProvider;
        _viesService = viesService;
    }

    public async Task<VATCalculationResult> CalculateVATAsync(
        string storeId,
        decimal netAmount,
        string currency,
        string customerCountry,
        string? customerVATNumber = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetStoreSettingsAsync(storeId, cancellationToken);

        if (settings == null || !settings.Enabled)
        {
            return CreateZeroVATResult(netAmount, currency, customerCountry, "VAT not enabled");
        }

        if (settings.BelowSmallBusinessThreshold)
        {
            return new VATCalculationResult
            {
                NetAmount = netAmount,
                VATRate = 0,
                VATAmount = 0,
                GrossAmount = netAmount,
                Currency = currency,
                CountryCode = customerCountry,
                CountryName = _rateProvider.GetRateInfo(customerCountry)?.CountryName ?? customerCountry,
                ReverseChargeApplied = false,
                ModeApplied = settings.Mode,
                BelowSmallBusinessThreshold = true
            };
        }

        // Determine the applicable country for VAT
        string vatCountry;
        if (settings.Mode == VATMode.Fixed)
        {
            vatCountry = settings.HomeCountry;
        }
        else // OSS mode
        {
            vatCountry = _rateProvider.IsEUCountry(customerCountry) ? customerCountry : settings.HomeCountry;
        }

        // Check for B2B reverse charge
        bool reverseChargeApplied = false;
        string? validatedVATNumber = null;

        if (!string.IsNullOrWhiteSpace(customerVATNumber) &&
            settings.ValidateVIES &&
            settings.Mode == VATMode.OSS &&
            customerCountry != settings.HomeCountry &&
            _rateProvider.IsEUCountry(customerCountry))
        {
            var viesResult = await _viesService.ValidateVATNumberAsync(customerVATNumber, cancellationToken);
            if (viesResult.IsValid)
            {
                reverseChargeApplied = true;
                validatedVATNumber = customerVATNumber;
            }
        }

        // Get the VAT rate
        decimal vatRate = 0;
        if (!reverseChargeApplied)
        {
            vatRate = _rateProvider.GetStandardRate(vatCountry) ?? 0;
        }

        // Calculate amounts
        decimal vatAmount = Math.Round(netAmount * vatRate / 100, 2);
        decimal grossAmount = netAmount + vatAmount;

        var rateInfo = _rateProvider.GetRateInfo(vatCountry);

        return new VATCalculationResult
        {
            NetAmount = netAmount,
            VATRate = vatRate,
            VATAmount = vatAmount,
            GrossAmount = grossAmount,
            Currency = currency,
            CountryCode = vatCountry,
            CountryName = rateInfo?.CountryName ?? vatCountry,
            ReverseChargeApplied = reverseChargeApplied,
            CustomerVATNumber = validatedVATNumber,
            ModeApplied = settings.Mode,
            BelowSmallBusinessThreshold = false
        };
    }

    public Task<VATInvoiceResult> CreateInvoiceWithVATAsync(
        string storeId,
        CreateVATInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        // This method is deprecated - invoice creation is now handled in the controller
        // using BTCPay's UIInvoiceController.CreateInvoiceCoreRaw
        throw new NotImplementedException("Use the controller's CreateInvoiceWithVAT endpoint instead");
    }

    public async Task SaveVATRecordAsync(
        string invoiceId,
        string storeId,
        VATCalculationResult vatResult,
        string customerCountry,
        string? customerVATNumber,
        CancellationToken cancellationToken = default)
    {
        var invoiceRecord = new VATInvoiceRecord
        {
            InvoiceId = invoiceId,
            StoreId = storeId,
            CustomerCountry = customerCountry,
            VATRate = vatResult.VATRate,
            NetAmount = vatResult.NetAmount,
            VATAmount = vatResult.VATAmount,
            GrossAmount = vatResult.GrossAmount,
            Currency = vatResult.Currency,
            ReverseChargeApplied = vatResult.ReverseChargeApplied,
            CustomerVATNumber = customerVATNumber,
            ModeApplied = vatResult.ModeApplied,
            EvidenceJson = JsonConvert.SerializeObject(new
            {
                customerCountry,
                customerVATNumber,
                calculatedAt = DateTimeOffset.UtcNow,
                vatCountryApplied = vatResult.CountryCode,
                vatRate = vatResult.VATRate
            })
        };

        // Add evidence
        invoiceRecord.Evidence.Add(new VATEvidence
        {
            Type = VATEvidenceType.BillingAddress,
            CountryCode = customerCountry,
            Confidence = 1.0m,
            CollectedAt = DateTimeOffset.UtcNow
        });

        _dbContext.VATInvoiceRecords.Add(invoiceRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<VATInvoiceRecord?> GetVATRecordAsync(
        string storeId,
        string invoiceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.VATInvoiceRecords
            .Include(r => r.Evidence)
            .FirstOrDefaultAsync(r => r.StoreId == storeId && r.InvoiceId == invoiceId, cancellationToken);
    }

    public async Task<VATStoreSettings?> GetStoreSettingsAsync(
        string storeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.VATStoreSettings
            .FirstOrDefaultAsync(s => s.StoreId == storeId, cancellationToken);
    }

    public async Task<VATStoreSettings> UpdateStoreSettingsAsync(
        string storeId,
        UpdateVATSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetStoreSettingsAsync(storeId, cancellationToken);

        if (settings == null)
        {
            settings = new VATStoreSettings { StoreId = storeId };
            _dbContext.VATStoreSettings.Add(settings);
        }

        settings.Mode = request.Mode;
        settings.HomeCountry = request.HomeCountry.ToUpperInvariant();
        settings.VATNumber = request.VATNumber;
        settings.Enabled = request.Enabled;
        settings.ValidateVIES = request.ValidateVIES;
        settings.BelowSmallBusinessThreshold = request.BelowSmallBusinessThreshold;
        settings.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return settings;
    }

    private VATCalculationResult CreateZeroVATResult(decimal netAmount, string currency, string customerCountry, string reason)
    {
        return new VATCalculationResult
        {
            NetAmount = netAmount,
            VATRate = 0,
            VATAmount = 0,
            GrossAmount = netAmount,
            Currency = currency,
            CountryCode = customerCountry,
            CountryName = _rateProvider.GetRateInfo(customerCountry)?.CountryName ?? customerCountry,
            ReverseChargeApplied = false,
            ModeApplied = VATMode.Fixed,
            BelowSmallBusinessThreshold = false
        };
    }
}
