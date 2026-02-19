using BTCPayServer.Plugins.VAT.Data.Models;

namespace BTCPayServer.Plugins.VAT.Models.Response;

public class VATCalculationResult
{
    /// <summary>
    /// Net amount before VAT
    /// </summary>
    public decimal NetAmount { get; set; }

    /// <summary>
    /// VAT rate applied as a percentage (e.g., 20.0 for 20%)
    /// </summary>
    public decimal VATRate { get; set; }

    /// <summary>
    /// VAT amount
    /// </summary>
    public decimal VATAmount { get; set; }

    /// <summary>
    /// Gross amount including VAT
    /// </summary>
    public decimal GrossAmount { get; set; }

    /// <summary>
    /// Currency code
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Country code where VAT is applied
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Country name where VAT is applied
    /// </summary>
    public string CountryName { get; set; } = string.Empty;

    /// <summary>
    /// Whether reverse charge was applied (B2B with validated VAT number)
    /// </summary>
    public bool ReverseChargeApplied { get; set; }

    /// <summary>
    /// Validated customer VAT number if reverse charge applied
    /// </summary>
    public string? CustomerVATNumber { get; set; }

    /// <summary>
    /// VAT mode used for calculation
    /// </summary>
    public VATMode ModeApplied { get; set; }

}
