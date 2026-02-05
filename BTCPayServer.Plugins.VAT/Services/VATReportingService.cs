using System.Globalization;
using System.Text;
using BTCPayServer.Client.Models;
using BTCPayServer.Plugins.VAT.Data;
using BTCPayServer.Plugins.VAT.Data.Models;
using BTCPayServer.Services.Invoices;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.VAT.Services;

public class VATReportingService
{
    private readonly VATDbContext _dbContext;
    private readonly VATRateProvider _rateProvider;
    private readonly InvoiceRepository _invoiceRepository;

    public VATReportingService(VATDbContext dbContext, VATRateProvider rateProvider, InvoiceRepository invoiceRepository)
    {
        _dbContext = dbContext;
        _rateProvider = rateProvider;
        _invoiceRepository = invoiceRepository;
    }

    /// <summary>
    /// Generates a VAT report for a store within a date range
    /// </summary>
    public async Task<VATReport> GenerateReportAsync(
        string storeId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        VATReportGrouping grouping = VATReportGrouping.ByCountry,
        CancellationToken cancellationToken = default)
    {
        var allRecords = await _dbContext.VATInvoiceRecords
            .Where(r => r.StoreId == storeId &&
                        r.CreatedAt >= startDate &&
                        r.CreatedAt < endDate)
            .ToListAsync(cancellationToken);

        // Only include records whose BTCPay invoice is settled or expired with late payment
        var invoiceIds = allRecords.Select(r => r.InvoiceId).ToArray();
        var includedInvoiceIds = new HashSet<string>();
        if (invoiceIds.Length > 0)
        {
            var invoices = await _invoiceRepository.GetInvoices(invoiceIds);
            foreach (var inv in invoices)
            {
                if (inv.Status == InvoiceStatus.Settled)
                    includedInvoiceIds.Add(inv.Id);
                else if (inv.Status == InvoiceStatus.Expired &&
                         inv.ExceptionStatus == InvoiceExceptionStatus.PaidLate)
                    includedInvoiceIds.Add(inv.Id);
            }
        }

        var records = allRecords.Where(r => includedInvoiceIds.Contains(r.InvoiceId)).ToList();

        var report = new VATReport
        {
            StoreId = storeId,
            StartDate = startDate,
            EndDate = endDate,
            GeneratedAt = DateTimeOffset.UtcNow,
            Grouping = grouping
        };

        // Calculate totals
        report.TotalNetAmount = records.Sum(r => r.NetAmount);
        report.TotalVATAmount = records.Sum(r => r.VATAmount);
        report.TotalGrossAmount = records.Sum(r => r.GrossAmount);
        report.InvoiceCount = records.Count;

        // Group records based on grouping type
        IEnumerable<IGrouping<string, VATInvoiceRecord>> groupedRecords = grouping switch
        {
            VATReportGrouping.ByCountry => records.GroupBy(r => r.CustomerCountry),
            VATReportGrouping.ByMonth => records.GroupBy(r => r.CreatedAt.ToString("yyyy-MM")),
            VATReportGrouping.ByQuarter => records.GroupBy(r => $"{r.CreatedAt.Year}-Q{(r.CreatedAt.Month - 1) / 3 + 1}"),
            VATReportGrouping.ByRate => records.GroupBy(r => r.VATRate.ToString("F2")),
            _ => records.GroupBy(r => r.CustomerCountry)
        };

        foreach (var group in groupedRecords.OrderBy(g => g.Key))
        {
            var groupRecords = group.ToList();
            var countryCode = grouping == VATReportGrouping.ByCountry ? group.Key : null;
            var rateInfo = countryCode != null ? _rateProvider.GetRateInfo(countryCode) : null;

            report.Items.Add(new VATReportItem
            {
                GroupKey = group.Key,
                GroupLabel = GetGroupLabel(grouping, group.Key, rateInfo),
                CountryCode = countryCode,
                CountryName = rateInfo?.CountryName,
                NetAmount = groupRecords.Sum(r => r.NetAmount),
                VATAmount = groupRecords.Sum(r => r.VATAmount),
                GrossAmount = groupRecords.Sum(r => r.GrossAmount),
                InvoiceCount = groupRecords.Count,
                AverageVATRate = groupRecords.Average(r => r.VATRate),
                ReverseChargeCount = groupRecords.Count(r => r.ReverseChargeApplied)
            });
        }

        // Calculate reverse charge totals
        report.ReverseChargeCount = records.Count(r => r.ReverseChargeApplied);
        report.ReverseChargeTotalNet = records.Where(r => r.ReverseChargeApplied).Sum(r => r.NetAmount);

        return report;
    }

    /// <summary>
    /// Exports the VAT report to CSV format
    /// </summary>
    public string ExportToCsv(VATReport report)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("VAT Report Export");
        sb.AppendLine($"Store ID,{report.StoreId}");
        sb.AppendLine($"Period,{report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd}");
        sb.AppendLine($"Generated,{report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Summary
        sb.AppendLine("Summary");
        sb.AppendLine($"Total Invoices,{report.InvoiceCount}");
        sb.AppendLine($"Total Net Amount,{report.TotalNetAmount:F2}");
        sb.AppendLine($"Total VAT Amount,{report.TotalVATAmount:F2}");
        sb.AppendLine($"Total Gross Amount,{report.TotalGrossAmount:F2}");
        sb.AppendLine($"Reverse Charge Transactions,{report.ReverseChargeCount}");
        sb.AppendLine();

        // Detail by group
        sb.AppendLine("Detail by " + report.Grouping);
        sb.AppendLine("Group,Country Code,Country Name,Invoices,Net Amount,VAT Amount,Gross Amount,Avg VAT Rate,Reverse Charge Count");

        foreach (var item in report.Items)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4:F2},{5:F2},{6:F2},{7:F2}%,{8}",
                EscapeCsvField(item.GroupLabel),
                item.CountryCode ?? "",
                EscapeCsvField(item.CountryName ?? ""),
                item.InvoiceCount,
                item.NetAmount,
                item.VATAmount,
                item.GrossAmount,
                item.AverageVATRate,
                item.ReverseChargeCount));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets VAT records for a store within a date range
    /// </summary>
    public async Task<List<VATInvoiceRecord>> GetRecordsAsync(
        string storeId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null,
        string? customerCountry = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VATInvoiceRecords
            .Where(r => r.StoreId == storeId);

        if (startDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt < endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(customerCountry))
        {
            query = query.Where(r => r.CustomerCountry == customerCountry);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static string GetGroupLabel(VATReportGrouping grouping, string key, VATRateInfo? rateInfo)
    {
        return grouping switch
        {
            VATReportGrouping.ByCountry => rateInfo?.CountryName ?? key,
            VATReportGrouping.ByMonth => key,
            VATReportGrouping.ByQuarter => key,
            VATReportGrouping.ByRate => $"{key}%",
            _ => key
        };
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return "";
        }

        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}

public enum VATReportGrouping
{
    ByCountry,
    ByMonth,
    ByQuarter,
    ByRate
}

public class VATReport
{
    public string StoreId { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public VATReportGrouping Grouping { get; set; }

    public decimal TotalNetAmount { get; set; }
    public decimal TotalVATAmount { get; set; }
    public decimal TotalGrossAmount { get; set; }
    public int InvoiceCount { get; set; }
    public int ReverseChargeCount { get; set; }
    public decimal ReverseChargeTotalNet { get; set; }

    public List<VATReportItem> Items { get; set; } = new();
}

public class VATReportItem
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
