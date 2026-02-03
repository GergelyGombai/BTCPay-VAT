using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BTCPayServer.Plugins.VAT.Models.Response;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.VAT.Services;

/// <summary>
/// Service for validating EU VAT numbers via the VIES (VAT Information Exchange System)
/// </summary>
public class VIESValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<VIESValidationService> _logger;
    private readonly VATRateProvider _rateProvider;

    // EU Commission VIES REST API endpoint
    private const string ViesApiUrl = "https://ec.europa.eu/taxation_customs/vies/rest-api/check-vat-number";

    // Regex patterns for VAT number validation by country
    private static readonly Dictionary<string, string> VATNumberPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AT"] = @"^ATU\d{8}$",
        ["BE"] = @"^BE0?\d{9,10}$",
        ["BG"] = @"^BG\d{9,10}$",
        ["HR"] = @"^HR\d{11}$",
        ["CY"] = @"^CY\d{8}[A-Z]$",
        ["CZ"] = @"^CZ\d{8,10}$",
        ["DK"] = @"^DK\d{8}$",
        ["EE"] = @"^EE\d{9}$",
        ["FI"] = @"^FI\d{8}$",
        ["FR"] = @"^FR[A-Z0-9]{2}\d{9}$",
        ["DE"] = @"^DE\d{9}$",
        ["GR"] = @"^EL\d{9}$",
        ["HU"] = @"^HU\d{8}$",
        ["IE"] = @"^IE\d{7}[A-Z]{1,2}$|^IE\d[A-Z]\d{5}[A-Z]$",
        ["IT"] = @"^IT\d{11}$",
        ["LV"] = @"^LV\d{11}$",
        ["LT"] = @"^LT(\d{9}|\d{12})$",
        ["LU"] = @"^LU\d{8}$",
        ["MT"] = @"^MT\d{8}$",
        ["NL"] = @"^NL\d{9}B\d{2}$",
        ["PL"] = @"^PL\d{10}$",
        ["PT"] = @"^PT\d{9}$",
        ["RO"] = @"^RO\d{2,10}$",
        ["SK"] = @"^SK\d{10}$",
        ["SI"] = @"^SI\d{8}$",
        ["ES"] = @"^ES[A-Z0-9]\d{7}[A-Z0-9]$",
        ["SE"] = @"^SE\d{12}$"
    };

    public VIESValidationService(
        HttpClient httpClient,
        ILogger<VIESValidationService> logger,
        VATRateProvider rateProvider)
    {
        _httpClient = httpClient;
        _logger = logger;
        _rateProvider = rateProvider;
    }

    /// <summary>
    /// Validates a VAT number via the EU VIES system
    /// </summary>
    public async Task<VIESValidationResponse> ValidateVATNumberAsync(
        string vatNumber,
        CancellationToken cancellationToken = default)
    {
        var response = new VIESValidationResponse
        {
            VATNumber = vatNumber,
            ValidationDate = DateTimeOffset.UtcNow
        };

        // Clean and normalize the VAT number
        var cleanVatNumber = NormalizeVATNumber(vatNumber);
        if (string.IsNullOrWhiteSpace(cleanVatNumber) || cleanVatNumber.Length < 4)
        {
            response.IsValid = false;
            response.ErrorMessage = "Invalid VAT number format";
            return response;
        }

        // Extract country code (first 2 characters)
        var countryCode = cleanVatNumber[..2].ToUpperInvariant();
        var number = cleanVatNumber[2..];

        // Map Greece's EL prefix to GR
        if (countryCode == "EL")
        {
            countryCode = "GR";
        }

        response.CountryCode = countryCode;

        // Validate format locally first
        if (!ValidateVATNumberFormat(cleanVatNumber))
        {
            response.IsValid = false;
            response.ErrorMessage = "VAT number format is invalid for the specified country";
            return response;
        }

        // Check if it's an EU country
        if (!_rateProvider.IsEUCountry(countryCode))
        {
            response.IsValid = false;
            response.ErrorMessage = $"Country code '{countryCode}' is not an EU member state";
            return response;
        }

        try
        {
            // Call VIES API
            var requestBody = new
            {
                countryCode = countryCode == "GR" ? "EL" : countryCode,
                vatNumber = number
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");
            var httpResponse = await _httpClient.PostAsync(ViesApiUrl, jsonContent, cancellationToken);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("VIES API returned status code {StatusCode}", httpResponse.StatusCode);
                response.IsValid = false;
                response.ErrorMessage = "VIES service is temporarily unavailable";
                return response;
            }

            var responseContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            var viesResult = JsonSerializer.Deserialize<ViesApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (viesResult == null)
            {
                response.IsValid = false;
                response.ErrorMessage = "Invalid response from VIES service";
                return response;
            }

            response.IsValid = viesResult.Valid;
            response.Name = viesResult.Name;
            response.Address = viesResult.Address;

            if (!viesResult.Valid)
            {
                response.ErrorMessage = "VAT number is not registered in VIES";
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to VIES service");
            response.IsValid = false;
            response.ErrorMessage = "Failed to connect to VIES service";
        }
        catch (TaskCanceledException)
        {
            response.IsValid = false;
            response.ErrorMessage = "VIES service request timed out";
        }

        return response;
    }

    /// <summary>
    /// Validates VAT number format locally without calling VIES
    /// </summary>
    public bool ValidateVATNumberFormat(string vatNumber)
    {
        var cleanVatNumber = NormalizeVATNumber(vatNumber);
        if (string.IsNullOrWhiteSpace(cleanVatNumber) || cleanVatNumber.Length < 4)
        {
            return false;
        }

        var countryCode = cleanVatNumber[..2].ToUpperInvariant();

        // Map Greece's EL to GR for lookup
        var lookupCode = countryCode == "EL" ? "GR" : countryCode;

        if (!VATNumberPatterns.TryGetValue(lookupCode, out var pattern))
        {
            return false;
        }

        return Regex.IsMatch(cleanVatNumber, pattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Extracts the country code from a VAT number
    /// </summary>
    public string? ExtractCountryCode(string vatNumber)
    {
        var cleanVatNumber = NormalizeVATNumber(vatNumber);
        if (string.IsNullOrWhiteSpace(cleanVatNumber) || cleanVatNumber.Length < 2)
        {
            return null;
        }

        var countryCode = cleanVatNumber[..2].ToUpperInvariant();

        // Map Greece's EL to GR
        if (countryCode == "EL")
        {
            countryCode = "GR";
        }

        return _rateProvider.IsEUCountry(countryCode) ? countryCode : null;
    }

    private static string NormalizeVATNumber(string vatNumber)
    {
        if (string.IsNullOrWhiteSpace(vatNumber))
        {
            return string.Empty;
        }

        // Remove spaces, dots, and dashes
        return Regex.Replace(vatNumber, @"[\s\.\-]", "").ToUpperInvariant();
    }

    private class ViesApiResponse
    {
        public bool Valid { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? CountryCode { get; set; }
        public string? VatNumber { get; set; }
    }
}
