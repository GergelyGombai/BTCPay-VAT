namespace BTCPayServer.Plugins.VAT.Models.Response;

public class VATRatesResponse
{
    /// <summary>
    /// List of all EU VAT rates
    /// </summary>
    public List<CountryVATRate> Rates { get; set; } = new();
}

public class CountryVATRate
{
    /// <summary>
    /// Country code (ISO 3166-1 alpha-2)
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Country name
    /// </summary>
    public string CountryName { get; set; } = string.Empty;

    /// <summary>
    /// Standard VAT rate as a percentage
    /// </summary>
    public decimal StandardRate { get; set; }

    /// <summary>
    /// Reduced VAT rate as a percentage (if applicable)
    /// </summary>
    public decimal? ReducedRate { get; set; }

    /// <summary>
    /// Super-reduced VAT rate as a percentage (if applicable)
    /// </summary>
    public decimal? SuperReducedRate { get; set; }
}
