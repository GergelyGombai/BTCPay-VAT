using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Plugins.VAT.Models.Request;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.VAT.Controllers;

[Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
[Route("stores/{storeId}/plugins/vat")]
public class UIVATController : Controller
{
    private readonly IVATCalculationService _vatService;
    private readonly VATRateProvider _rateProvider;
    private readonly VATReportingService _reportingService;

    public UIVATController(
        IVATCalculationService vatService,
        VATRateProvider rateProvider,
        VATReportingService reportingService)
    {
        _vatService = vatService;
        _rateProvider = rateProvider;
        _reportingService = reportingService;
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> Settings(string storeId)
    {
        var settings = await _vatService.GetStoreSettingsAsync(storeId);
        var countries = _rateProvider.GetAllRates()
            .Select(kvp => new { Code = kvp.Key, Name = kvp.Value.CountryName, Rate = kvp.Value.StandardRate })
            .OrderBy(c => c.Name)
            .ToList();

        var html = GenerateSettingsHtml(storeId, settings, countries);
        return Content(html, "text/html");
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanModifyStoreSettings)]
    public async Task<IActionResult> Settings(string storeId, [FromForm] VATSettingsFormModel model)
    {
        if (!_rateProvider.IsEUCountry(model.HomeCountry))
        {
            return BadRequest($"'{model.HomeCountry}' is not a valid EU country code");
        }

        var request = new UpdateVATSettingsRequest
        {
            Mode = model.Mode,
            HomeCountry = model.HomeCountry,
            VATNumber = model.VATNumber,
            Enabled = model.Enabled,
            ValidateVIES = model.ValidateVIES,
            BelowSmallBusinessThreshold = model.BelowSmallBusinessThreshold
        };

        await _vatService.UpdateStoreSettingsAsync(storeId, request);

        return Redirect($"/stores/{storeId}/plugins/vat?success=1");
    }

    [HttpGet("reports")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> Reports(string storeId, DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, VATReportGrouping.ByCountry);
        var html = GenerateReportsHtml(storeId, start, end, report);
        return Content(html, "text/html");
    }

    [HttpGet("reports/export")]
    [Authorize(Policy = Policies.CanViewStoreSettings)]
    public async Task<IActionResult> ExportReport(string storeId, DateTimeOffset? startDate, DateTimeOffset? endDate)
    {
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, VATReportGrouping.ByCountry);
        var csv = _reportingService.ExportToCsv(report);

        var fileName = $"vat-report-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    private string GenerateSettingsHtml(string storeId, VATStoreSettings? settings, dynamic countries)
    {
        var mode = settings?.Mode ?? VATMode.Fixed;
        var homeCountry = settings?.HomeCountry ?? "DE";
        var vatNumber = settings?.VATNumber ?? "";
        var enabled = settings?.Enabled ?? false;
        var validateVIES = settings?.ValidateVIES ?? true;
        var belowThreshold = settings?.BelowSmallBusinessThreshold ?? false;
        var showSuccess = Request.Query.ContainsKey("success");

        var countryOptions = string.Join("\n", ((IEnumerable<dynamic>)countries).Select(c =>
            $"<option value=\"{c.Code}\" {(c.Code == homeCountry ? "selected" : "")}>{c.Name} ({c.Code}) - {c.Rate}%</option>"));

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>VAT Settings - BTCPay Server</title>
    <link href=""/main/bootstrap/bootstrap.css"" rel=""stylesheet"" />
    <link href=""/main/layout.css"" rel=""stylesheet"" />
    <link href=""/main/site.css"" rel=""stylesheet"" />
</head>
<body>
    <div class=""container mt-4"">
        <nav aria-label=""breadcrumb"">
            <ol class=""breadcrumb"">
                <li class=""breadcrumb-item""><a href=""/stores/{storeId}"">Store</a></li>
                <li class=""breadcrumb-item active"">VAT Settings</li>
            </ol>
        </nav>

        <h2 class=""mb-4"">VAT Settings</h2>

        {(showSuccess ? @"<div class=""alert alert-success alert-dismissible fade show"" role=""alert"">
            VAT settings saved successfully.
            <button type=""button"" class=""btn-close"" data-bs-dismiss=""alert""></button>
        </div>" : "")}

        <div class=""row"">
            <div class=""col-xl-8"">
                <form method=""post"">
                    <div class=""mb-3"">
                        <div class=""form-check form-switch"">
                            <input type=""checkbox"" class=""form-check-input"" id=""Enabled"" name=""Enabled"" value=""true"" {(enabled ? "checked" : "")} />
                            <label class=""form-check-label"" for=""Enabled""><strong>Enable VAT calculation</strong></label>
                        </div>
                        <div class=""form-text"">When enabled, VAT will be calculated for invoices created via the VAT API.</div>
                    </div>

                    <div class=""mb-3"">
                        <label for=""Mode"" class=""form-label"">VAT Mode</label>
                        <select class=""form-select"" id=""Mode"" name=""Mode"">
                            <option value=""0"" {(mode == VATMode.Fixed ? "selected" : "")}>Fixed - Single country VAT rate</option>
                            <option value=""1"" {(mode == VATMode.OSS ? "selected" : "")}>OSS - Customer location-based VAT</option>
                        </select>
                        <div class=""form-text"">
                            <strong>Fixed:</strong> Always charges your home country's VAT rate.<br/>
                            <strong>OSS:</strong> Charges VAT based on the customer's EU country (One-Stop Shop).
                        </div>
                    </div>

                    <div class=""mb-3"">
                        <label for=""HomeCountry"" class=""form-label"">Home Country</label>
                        <select class=""form-select"" id=""HomeCountry"" name=""HomeCountry"">
                            {countryOptions}
                        </select>
                        <div class=""form-text"">Your business registration country.</div>
                    </div>

                    <div class=""mb-3"">
                        <label for=""VATNumber"" class=""form-label"">VAT Registration Number</label>
                        <input type=""text"" class=""form-control"" id=""VATNumber"" name=""VATNumber"" value=""{System.Web.HttpUtility.HtmlEncode(vatNumber)}"" placeholder=""e.g., DE123456789"" />
                        <div class=""form-text"">Your business VAT identification number.</div>
                    </div>

                    <div class=""mb-3"">
                        <div class=""form-check"">
                            <input type=""checkbox"" class=""form-check-input"" id=""ValidateVIES"" name=""ValidateVIES"" value=""true"" {(validateVIES ? "checked" : "")} />
                            <label class=""form-check-label"" for=""ValidateVIES"">Validate B2B VAT numbers via VIES</label>
                        </div>
                        <div class=""form-text"">Valid B2B transactions will have reverse charge applied (0% VAT).</div>
                    </div>

                    <div class=""mb-3"">
                        <div class=""form-check"">
                            <input type=""checkbox"" class=""form-check-input"" id=""BelowSmallBusinessThreshold"" name=""BelowSmallBusinessThreshold"" value=""true"" {(belowThreshold ? "checked" : "")} />
                            <label class=""form-check-label"" for=""BelowSmallBusinessThreshold"">Below small business threshold (Kleinunternehmerregelung)</label>
                        </div>
                        <div class=""form-text"">If enabled, no VAT will be charged.</div>
                    </div>

                    <div class=""mt-4"">
                        <button type=""submit"" class=""btn btn-primary"">Save Settings</button>
                        <a href=""/stores/{storeId}/plugins/vat/reports"" class=""btn btn-secondary ms-2"">View Reports</a>
                    </div>
                </form>
            </div>
        </div>

        <div class=""row mt-5"">
            <div class=""col-xl-8"">
                <h4>API Endpoints</h4>
                <table class=""table table-sm"">
                    <thead><tr><th>Method</th><th>Endpoint</th><th>Description</th></tr></thead>
                    <tbody>
                        <tr><td><code>POST</code></td><td><code>/api/v1/stores/{{storeId}}/vat/calculate</code></td><td>Preview VAT calculation</td></tr>
                        <tr><td><code>POST</code></td><td><code>/api/v1/stores/{{storeId}}/vat/invoices</code></td><td>Create invoice with VAT</td></tr>
                        <tr><td><code>GET</code></td><td><code>/api/v1/stores/{{storeId}}/vat/reports</code></td><td>Generate VAT report</td></tr>
                        <tr><td><code>GET</code></td><td><code>/api/v1/vat/rates</code></td><td>Get all EU VAT rates</td></tr>
                        <tr><td><code>GET</code></td><td><code>/api/v1/vat/validate/{{vatNumber}}</code></td><td>Validate VAT number via VIES</td></tr>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
    <script src=""/main/bootstrap/bootstrap.bundle.min.js""></script>
</body>
</html>";
    }

    private string GenerateReportsHtml(string storeId, DateTimeOffset startDate, DateTimeOffset endDate, VATReport report)
    {
        var rows = string.Join("\n", report.Items.Select(item => $@"
            <tr>
                <td>{System.Web.HttpUtility.HtmlEncode(item.GroupLabel)} ({item.CountryCode})</td>
                <td class=""text-end"">{item.InvoiceCount}</td>
                <td class=""text-end"">{item.NetAmount:N2}</td>
                <td class=""text-end"">{item.VATAmount:N2}</td>
                <td class=""text-end"">{item.GrossAmount:N2}</td>
                <td class=""text-end"">{item.AverageVATRate:N1}%</td>
            </tr>"));

        if (string.IsNullOrEmpty(rows))
        {
            rows = @"<tr><td colspan=""6"" class=""text-center text-muted py-4"">No VAT transactions found for the selected period.</td></tr>";
        }

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>VAT Reports - BTCPay Server</title>
    <link href=""/main/bootstrap/bootstrap.css"" rel=""stylesheet"" />
    <link href=""/main/layout.css"" rel=""stylesheet"" />
    <link href=""/main/site.css"" rel=""stylesheet"" />
</head>
<body>
    <div class=""container mt-4"">
        <nav aria-label=""breadcrumb"">
            <ol class=""breadcrumb"">
                <li class=""breadcrumb-item""><a href=""/stores/{storeId}"">Store</a></li>
                <li class=""breadcrumb-item""><a href=""/stores/{storeId}/plugins/vat"">VAT Settings</a></li>
                <li class=""breadcrumb-item active"">Reports</li>
            </ol>
        </nav>

        <h2 class=""mb-4"">VAT Reports</h2>

        <form method=""get"" class=""mb-4"">
            <div class=""row g-3 align-items-end"">
                <div class=""col-auto"">
                    <label class=""form-label"">Start Date</label>
                    <input type=""date"" name=""startDate"" value=""{startDate:yyyy-MM-dd}"" class=""form-control"" />
                </div>
                <div class=""col-auto"">
                    <label class=""form-label"">End Date</label>
                    <input type=""date"" name=""endDate"" value=""{endDate:yyyy-MM-dd}"" class=""form-control"" />
                </div>
                <div class=""col-auto"">
                    <button type=""submit"" class=""btn btn-primary"">Generate</button>
                </div>
                <div class=""col-auto"">
                    <a href=""/stores/{storeId}/plugins/vat/reports/export?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}"" class=""btn btn-secondary"">Export CSV</a>
                </div>
            </div>
        </form>

        <div class=""card mb-4"">
            <div class=""card-header""><strong>Summary</strong></div>
            <div class=""card-body"">
                <div class=""row"">
                    <div class=""col-md-3"">
                        <div class=""text-muted small"">Total Invoices</div>
                        <div class=""fs-4 fw-bold"">{report.InvoiceCount}</div>
                    </div>
                    <div class=""col-md-3"">
                        <div class=""text-muted small"">Total Net</div>
                        <div class=""fs-4 fw-bold"">{report.TotalNetAmount:N2}</div>
                    </div>
                    <div class=""col-md-3"">
                        <div class=""text-muted small"">Total VAT</div>
                        <div class=""fs-4 fw-bold text-primary"">{report.TotalVATAmount:N2}</div>
                    </div>
                    <div class=""col-md-3"">
                        <div class=""text-muted small"">Total Gross</div>
                        <div class=""fs-4 fw-bold"">{report.TotalGrossAmount:N2}</div>
                    </div>
                </div>
            </div>
        </div>

        <div class=""card"">
            <div class=""card-header""><strong>By Country</strong></div>
            <div class=""table-responsive"">
                <table class=""table table-hover mb-0"">
                    <thead>
                        <tr>
                            <th>Country</th>
                            <th class=""text-end"">Invoices</th>
                            <th class=""text-end"">Net Amount</th>
                            <th class=""text-end"">VAT Amount</th>
                            <th class=""text-end"">Gross Amount</th>
                            <th class=""text-end"">Avg Rate</th>
                        </tr>
                    </thead>
                    <tbody>
                        {rows}
                    </tbody>
                </table>
            </div>
        </div>

        <div class=""mt-4"">
            <a href=""/stores/{storeId}/plugins/vat"" class=""btn btn-secondary"">Back to Settings</a>
        </div>
    </div>
    <script src=""/main/bootstrap/bootstrap.bundle.min.js""></script>
</body>
</html>";
    }
}

public class VATSettingsFormModel
{
    public VATMode Mode { get; set; }
    public string HomeCountry { get; set; } = "";
    public string? VATNumber { get; set; }
    public bool Enabled { get; set; }
    public bool ValidateVIES { get; set; }
    public bool BelowSmallBusinessThreshold { get; set; }
}
