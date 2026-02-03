namespace BTCPayServer.Plugins.VAT.Models.Response;

public class VIESValidationResponse
{
    /// <summary>
    /// The VAT number that was validated
    /// </summary>
    public string VATNumber { get; set; } = string.Empty;

    /// <summary>
    /// Country code extracted from VAT number
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether the VAT number is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Business name registered to this VAT number (if available)
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Business address registered to this VAT number (if available)
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Date of validation
    /// </summary>
    public DateTimeOffset ValidationDate { get; set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
