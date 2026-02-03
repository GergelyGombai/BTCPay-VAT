using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Models.Response;

namespace BTCPayServer.Plugins.VAT.Services;

public interface IVATCalculationService
{
    /// <summary>
    /// Calculates VAT for a given amount based on store settings and customer information
    /// </summary>
    Task<VATCalculationResult> CalculateVATAsync(
        string storeId,
        decimal netAmount,
        string currency,
        string customerCountry,
        string? customerVATNumber = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an invoice with VAT calculation and records it for compliance
    /// </summary>
    Task<VATInvoiceResult> CreateInvoiceWithVATAsync(
        string storeId,
        CreateVATInvoiceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets VAT details for an existing invoice
    /// </summary>
    Task<VATInvoiceRecord?> GetVATRecordAsync(
        string storeId,
        string invoiceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets VAT store settings
    /// </summary>
    Task<VATStoreSettings?> GetStoreSettingsAsync(
        string storeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates VAT store settings
    /// </summary>
    Task<VATStoreSettings> UpdateStoreSettingsAsync(
        string storeId,
        UpdateVATSettingsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a VAT record for an invoice
    /// </summary>
    Task SaveVATRecordAsync(
        string invoiceId,
        string storeId,
        VATCalculationResult vatResult,
        string customerCountry,
        string? customerVATNumber,
        CancellationToken cancellationToken = default);
}
