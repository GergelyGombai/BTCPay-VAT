using System.ComponentModel.DataAnnotations;

namespace BTCPayServer.Plugins.VAT.Data.Models;

public class VATStoreSettings
{
    [Key]
    [MaxLength(50)]
    public string StoreId { get; set; } = string.Empty;

    public VATMode Mode { get; set; } = VATMode.Fixed;

    /// <summary>
    /// Two-letter ISO country code for the business's home country (e.g., "DE", "FR")
    /// </summary>
    [MaxLength(2)]
    public string HomeCountry { get; set; } = string.Empty;

    /// <summary>
    /// Business VAT registration number
    /// </summary>
    [MaxLength(20)]
    public string? VATNumber { get; set; }

    /// <summary>
    /// Whether VAT calculation is enabled for this store
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether to validate customer VAT numbers via VIES for B2B reverse charge
    /// </summary>
    public bool ValidateVIES { get; set; } = true;

    /// <summary>
    /// If true, business is below small business threshold and doesn't charge VAT
    /// This is managed manually by the merchant
    /// </summary>
    public bool BelowSmallBusinessThreshold { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
