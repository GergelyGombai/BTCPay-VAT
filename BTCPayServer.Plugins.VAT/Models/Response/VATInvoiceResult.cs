using BTCPayServer.Plugins.VAT.Data.Models;

namespace BTCPayServer.Plugins.VAT.Models.Response;

public class VATInvoiceResult
{
    /// <summary>
    /// BTCPay Server Invoice ID
    /// </summary>
    public string InvoiceId { get; set; } = string.Empty;

    /// <summary>
    /// Invoice checkout URL
    /// </summary>
    public string CheckoutUrl { get; set; } = string.Empty;

    /// <summary>
    /// Invoice status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// VAT calculation details
    /// </summary>
    public VATCalculationResult VAT { get; set; } = new();

    /// <summary>
    /// Invoice creation timestamp
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Invoice expiration timestamp
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
