using BTCPayServer.Plugins.VAT.Data.Models;

namespace BTCPayServer.Plugins.VAT.Models.Response;

public class VATSettingsResponse
{
    /// <summary>
    /// Store ID
    /// </summary>
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// VAT calculation mode: Fixed or OSS
    /// </summary>
    public VATMode Mode { get; set; }

    /// <summary>
    /// Business's home country code
    /// </summary>
    public string HomeCountry { get; set; } = string.Empty;

    /// <summary>
    /// Business's home country name
    /// </summary>
    public string HomeCountryName { get; set; } = string.Empty;

    /// <summary>
    /// Business VAT registration number
    /// </summary>
    public string? VATNumber { get; set; }

    /// <summary>
    /// Whether VAT calculation is enabled
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Whether VIES validation is enabled for B2B reverse charge
    /// </summary>
    public bool ValidateVIES { get; set; }

    /// <summary>
    /// Standard VAT rate for home country (for Fixed mode)
    /// </summary>
    public decimal HomeCountryVATRate { get; set; }
}
