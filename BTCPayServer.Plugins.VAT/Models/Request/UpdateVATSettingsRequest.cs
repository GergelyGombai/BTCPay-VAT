using System.ComponentModel.DataAnnotations;
using BTCPayServer.Plugins.VAT.Data.Models;

namespace BTCPayServer.Plugins.VAT.Models.Request;

public class UpdateVATSettingsRequest
{
    /// <summary>
    /// VAT calculation mode: Fixed (single rate) or OSS (customer location-based)
    /// </summary>
    [Required]
    public VATMode Mode { get; set; }

    /// <summary>
    /// Business's home country code (ISO 3166-1 alpha-2, e.g., "DE", "FR")
    /// </summary>
    [Required]
    [StringLength(2, MinimumLength = 2)]
    public string HomeCountry { get; set; } = string.Empty;

    /// <summary>
    /// Business VAT registration number
    /// </summary>
    [StringLength(20)]
    public string? VATNumber { get; set; }

    /// <summary>
    /// Whether VAT calculation is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to validate customer VAT numbers via VIES for B2B reverse charge
    /// </summary>
    public bool ValidateVIES { get; set; } = true;

    /// <summary>
    /// If true, business is below small business threshold and doesn't charge VAT
    /// </summary>
    public bool BelowSmallBusinessThreshold { get; set; }
}
