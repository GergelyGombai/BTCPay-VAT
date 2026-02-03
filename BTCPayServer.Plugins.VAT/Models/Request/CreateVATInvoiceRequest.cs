using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.VAT.Models.Request;

public class CreateVATInvoiceRequest
{
    /// <summary>
    /// Net amount before VAT
    /// </summary>
    [Required]
    [Range(0.00000001, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code (e.g., "EUR", "USD")
    /// </summary>
    [Required]
    [StringLength(10, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Customer's country code (ISO 3166-1 alpha-2, e.g., "DE", "FR")
    /// </summary>
    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string CustomerCountry { get; set; } = string.Empty;

    /// <summary>
    /// Customer's VAT number for B2B reverse charge (optional)
    /// </summary>
    [StringLength(20)]
    public string? CustomerVATNumber { get; set; }

    /// <summary>
    /// Customer's email address
    /// </summary>
    [EmailAddress]
    public string? BuyerEmail { get; set; }

    /// <summary>
    /// Customer's name
    /// </summary>
    public string? BuyerName { get; set; }

    /// <summary>
    /// Optional order ID for reference
    /// </summary>
    public string? OrderId { get; set; }

    /// <summary>
    /// Additional metadata to include in the invoice
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// URL to redirect after successful payment
    /// </summary>
    public string? RedirectUrl { get; set; }

    /// <summary>
    /// Checkout options
    /// </summary>
    public VATInvoiceCheckoutOptions? Checkout { get; set; }
}

public class VATInvoiceCheckoutOptions
{
    /// <summary>
    /// Expiration time in minutes (default: 15)
    /// </summary>
    public int? ExpirationMinutes { get; set; }

    /// <summary>
    /// Payment methods to enable
    /// </summary>
    public string[]? PaymentMethods { get; set; }

}
