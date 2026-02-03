using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.VAT.Data;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.VAT;

public class VATPlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    [
        new() { Identifier = nameof(BTCPayServer), Condition = ">=2.0.0" }
    ];

    public override void Execute(IServiceCollection services)
    {
        services.AddDbContext<VATDbContext>((provider, builder) =>
        {
            var dbOptions = provider.GetRequiredService<IOptions<DatabaseOptions>>();
            builder.UseNpgsql(dbOptions.Value.ConnectionString);
        });

        services.AddSingleton<VATRateProvider>();
        services.AddScoped<IVATCalculationService, VATCalculationService>();
        services.AddScoped<VATReportingService>();
        services.AddHttpClient<VIESValidationService>();

        services.AddHostedService<VATPluginMigrationRunner>();
    }
}

public class VATPluginMigrationRunner : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VATPluginMigrationRunner> _logger;

    public VATPluginMigrationRunner(IServiceProvider serviceProvider, ILogger<VATPluginMigrationRunner> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<VATDbContext>();

            // Create schema and tables using raw SQL
            await context.Database.ExecuteSqlRawAsync(CreateSchemaSql, cancellationToken);
            _logger.LogInformation("VAT Plugin database schema created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create VAT Plugin database schema");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private const string CreateSchemaSql = @"
        CREATE SCHEMA IF NOT EXISTS ""BTCPayServer.Plugins.VAT"";

        CREATE TABLE IF NOT EXISTS ""BTCPayServer.Plugins.VAT"".""VATStoreSettings"" (
            ""StoreId"" VARCHAR(50) PRIMARY KEY,
            ""Mode"" INTEGER NOT NULL DEFAULT 0,
            ""HomeCountry"" VARCHAR(2) NOT NULL DEFAULT '',
            ""VATNumber"" VARCHAR(20),
            ""Enabled"" BOOLEAN NOT NULL DEFAULT FALSE,
            ""ValidateVIES"" BOOLEAN NOT NULL DEFAULT TRUE,
            ""BelowSmallBusinessThreshold"" BOOLEAN NOT NULL DEFAULT FALSE,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            ""UpdatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS ""BTCPayServer.Plugins.VAT"".""VATInvoiceRecords"" (
            ""InvoiceId"" VARCHAR(50) PRIMARY KEY,
            ""StoreId"" VARCHAR(50) NOT NULL,
            ""CustomerCountry"" VARCHAR(2) NOT NULL,
            ""VATRate"" DECIMAL(5,2) NOT NULL,
            ""NetAmount"" DECIMAL(18,8) NOT NULL,
            ""VATAmount"" DECIMAL(18,8) NOT NULL,
            ""GrossAmount"" DECIMAL(18,8) NOT NULL,
            ""Currency"" VARCHAR(10) NOT NULL,
            ""ReverseChargeApplied"" BOOLEAN NOT NULL DEFAULT FALSE,
            ""CustomerVATNumber"" VARCHAR(20),
            ""ModeApplied"" INTEGER NOT NULL DEFAULT 0,
            ""EvidenceJson"" TEXT,
            ""CreatedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS ""BTCPayServer.Plugins.VAT"".""VATEvidence"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""VATInvoiceRecordId"" VARCHAR(50) NOT NULL REFERENCES ""BTCPayServer.Plugins.VAT"".""VATInvoiceRecords""(""InvoiceId"") ON DELETE CASCADE,
            ""Type"" INTEGER NOT NULL DEFAULT 0,
            ""CountryCode"" VARCHAR(2) NOT NULL,
            ""Confidence"" DECIMAL(3,2) NOT NULL DEFAULT 1.0,
            ""RawData"" TEXT,
            ""CollectedAt"" TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );

        CREATE INDEX IF NOT EXISTS ""IX_VATInvoiceRecords_StoreId"" ON ""BTCPayServer.Plugins.VAT"".""VATInvoiceRecords""(""StoreId"");
        CREATE INDEX IF NOT EXISTS ""IX_VATInvoiceRecords_CustomerCountry"" ON ""BTCPayServer.Plugins.VAT"".""VATInvoiceRecords""(""CustomerCountry"");
        CREATE INDEX IF NOT EXISTS ""IX_VATInvoiceRecords_CreatedAt"" ON ""BTCPayServer.Plugins.VAT"".""VATInvoiceRecords""(""CreatedAt"");
        CREATE INDEX IF NOT EXISTS ""IX_VATEvidence_VATInvoiceRecordId"" ON ""BTCPayServer.Plugins.VAT"".""VATEvidence""(""VATInvoiceRecordId"");
    ";
}
