using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.VAT.Models.Request;

public class CalculateVATRequest
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
}
