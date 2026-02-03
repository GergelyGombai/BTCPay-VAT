namespace BTCPayServer.Plugins.VAT.Services;

/// <summary>
/// Provides standard VAT rates for all 27 EU member states (2026 rates)
/// </summary>
public class VATRateProvider
{
    private static readonly Dictionary<string, VATRateInfo> EUVATRates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AT"] = new("Austria", 20.0m, 13.0m, 10.0m),
        ["BE"] = new("Belgium", 21.0m, 12.0m, 6.0m),
        ["BG"] = new("Bulgaria", 20.0m, 9.0m, null),
        ["HR"] = new("Croatia", 25.0m, 13.0m, 5.0m),
        ["CY"] = new("Cyprus", 19.0m, 9.0m, 5.0m),
        ["CZ"] = new("Czech Republic", 21.0m, 15.0m, 10.0m),
        ["DK"] = new("Denmark", 25.0m, null, null),
        ["EE"] = new("Estonia", 24.0m, 13.0m, 9.0m),
        ["FI"] = new("Finland", 25.5m, 14.0m, 10.0m),
        ["FR"] = new("France", 20.0m, 10.0m, 5.5m),
        ["DE"] = new("Germany", 19.0m, 7.0m, null),
        ["GR"] = new("Greece", 24.0m, 13.0m, 6.0m),
        ["HU"] = new("Hungary", 27.0m, 18.0m, 5.0m),
        ["IE"] = new("Ireland", 23.0m, 13.5m, 9.0m),
        ["IT"] = new("Italy", 22.0m, 10.0m, 5.0m),
        ["LV"] = new("Latvia", 21.0m, 12.0m, 5.0m),
        ["LT"] = new("Lithuania", 21.0m, 9.0m, 5.0m),
        ["LU"] = new("Luxembourg", 17.0m, 14.0m, 8.0m),
        ["MT"] = new("Malta", 18.0m, 7.0m, 5.0m),
        ["NL"] = new("Netherlands", 21.0m, 9.0m, null),
        ["PL"] = new("Poland", 23.0m, 8.0m, 5.0m),
        ["PT"] = new("Portugal", 23.0m, 13.0m, 6.0m),
        ["RO"] = new("Romania", 19.0m, 9.0m, 5.0m),
        ["SK"] = new("Slovakia", 23.0m, 10.0m, 5.0m),
        ["SI"] = new("Slovenia", 22.0m, 9.5m, 5.0m),
        ["ES"] = new("Spain", 21.0m, 10.0m, 4.0m),
        ["SE"] = new("Sweden", 25.0m, 12.0m, 6.0m)
    };

    /// <summary>
    /// Gets the standard VAT rate for an EU country
    /// </summary>
    /// <param name="countryCode">Two-letter ISO country code</param>
    /// <returns>Standard VAT rate as a percentage, or null if country not found</returns>
    public decimal? GetStandardRate(string countryCode)
    {
        return EUVATRates.TryGetValue(countryCode, out var rate) ? rate.StandardRate : null;
    }

    /// <summary>
    /// Gets all VAT rate information for an EU country
    /// </summary>
    public VATRateInfo? GetRateInfo(string countryCode)
    {
        return EUVATRates.TryGetValue(countryCode, out var rate) ? rate : null;
    }

    /// <summary>
    /// Gets all EU VAT rates
    /// </summary>
    public IReadOnlyDictionary<string, VATRateInfo> GetAllRates()
    {
        return EUVATRates;
    }

    /// <summary>
    /// Checks if a country code is a valid EU member state
    /// </summary>
    public bool IsEUCountry(string countryCode)
    {
        return EUVATRates.ContainsKey(countryCode);
    }

    /// <summary>
    /// Gets all EU country codes
    /// </summary>
    public IEnumerable<string> GetEUCountryCodes()
    {
        return EUVATRates.Keys;
    }
}

public record VATRateInfo(
    string CountryName,
    decimal StandardRate,
    decimal? ReducedRate,
    decimal? SuperReducedRate
);
