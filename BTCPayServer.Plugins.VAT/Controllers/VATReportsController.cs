using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Client;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.VAT.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
[EnableCors(CorsPolicies.All)]
public class VATReportsController : ControllerBase
{
    private readonly VATReportingService _reportingService;

    public VATReportsController(VATReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    /// <summary>
    /// Generate a VAT report for a store
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/vat/reports")]
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<ActionResult<VATReportResponse>> GetReport(
        string storeId,
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] VATReportGrouping grouping = VATReportGrouping.ByCountry,
        CancellationToken cancellationToken = default)
    {
        // Default to current month if no dates provided
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, grouping, cancellationToken);

        return Ok(MapToResponse(report));
    }

    /// <summary>
    /// Export VAT report as CSV
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/vat/reports/export")]
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<IActionResult> ExportReport(
        string storeId,
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] VATReportGrouping grouping = VATReportGrouping.ByCountry,
        CancellationToken cancellationToken = default)
    {
        var start = startDate ?? new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endDate ?? start.AddMonths(1);

        var report = await _reportingService.GenerateReportAsync(storeId, start, end, grouping, cancellationToken);
        var csv = _reportingService.ExportToCsv(report);

        var fileName = $"vat-report-{storeId}-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}.csv";

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    /// <summary>
    /// Get VAT records (transactions) for a store
    /// </summary>
    [HttpGet("~/api/v1/stores/{storeId}/vat/records")]
    [Authorize(Policy = Policies.CanViewStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Greenfield)]
    public async Task<ActionResult<VATRecordsResponse>> GetRecords(
        string storeId,
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] string? customerCountry,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (take > 500) take = 500;

        var records = await _reportingService.GetRecordsAsync(
            storeId, startDate, endDate, customerCountry, skip, take, cancellationToken);

        return Ok(new VATRecordsResponse
        {
            Records = records.Select(r => new VATRecordResponse
            {
                InvoiceId = r.InvoiceId,
                CustomerCountry = r.CustomerCountry,
                VATRate = r.VATRate,
                NetAmount = r.NetAmount,
                VATAmount = r.VATAmount,
                GrossAmount = r.GrossAmount,
                Currency = r.Currency,
                ReverseChargeApplied = r.ReverseChargeApplied,
                CustomerVATNumber = r.CustomerVATNumber,
                ModeApplied = r.ModeApplied.ToString(),
                CreatedAt = r.CreatedAt
            }).ToList(),
            Skip = skip,
            Take = take
        });
    }

    private static VATReportResponse MapToResponse(VATReport report)
    {
        return new VATReportResponse
        {
            StoreId = report.StoreId,
            StartDate = report.StartDate,
            EndDate = report.EndDate,
            GeneratedAt = report.GeneratedAt,
            Grouping = report.Grouping.ToString(),
            Summary = new VATReportSummary
            {
                TotalNetAmount = report.TotalNetAmount,
                TotalVATAmount = report.TotalVATAmount,
                TotalGrossAmount = report.TotalGrossAmount,
                InvoiceCount = report.InvoiceCount,
                ReverseChargeCount = report.ReverseChargeCount,
                ReverseChargeTotalNet = report.ReverseChargeTotalNet
            },
            Items = report.Items.Select(i => new VATReportItemResponse
            {
                GroupKey = i.GroupKey,
                GroupLabel = i.GroupLabel,
                CountryCode = i.CountryCode,
                CountryName = i.CountryName,
                NetAmount = i.NetAmount,
                VATAmount = i.VATAmount,
                GrossAmount = i.GrossAmount,
                InvoiceCount = i.InvoiceCount,
                AverageVATRate = i.AverageVATRate,
                ReverseChargeCount = i.ReverseChargeCount
            }).ToList()
        };
    }
}

public class VATReportResponse
{
    public string StoreId { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string Grouping { get; set; } = string.Empty;
    public VATReportSummary Summary { get; set; } = new();
    public List<VATReportItemResponse> Items { get; set; } = new();
}

public class VATReportSummary
{
    public decimal TotalNetAmount { get; set; }
    public decimal TotalVATAmount { get; set; }
    public decimal TotalGrossAmount { get; set; }
    public int InvoiceCount { get; set; }
    public int ReverseChargeCount { get; set; }
    public decimal ReverseChargeTotalNet { get; set; }
}

public class VATReportItemResponse
{
    public string GroupKey { get; set; } = string.Empty;
    public string GroupLabel { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public int InvoiceCount { get; set; }
    public decimal AverageVATRate { get; set; }
    public int ReverseChargeCount { get; set; }
}

public class VATRecordsResponse
{
    public List<VATRecordResponse> Records { get; set; } = new();
    public int Skip { get; set; }
    public int Take { get; set; }
}

public class VATRecordResponse
{
    public string InvoiceId { get; set; } = string.Empty;
    public string CustomerCountry { get; set; } = string.Empty;
    public decimal VATRate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool ReverseChargeApplied { get; set; }
    public string? CustomerVATNumber { get; set; }
    public string ModeApplied { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
