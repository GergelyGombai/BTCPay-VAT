using BTCPayServer;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.VAT.Data;
using BTCPayServer.Plugins.VAT.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        services.AddUIExtension("header-nav", "VAT/VATNav");

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

    public VATPluginMigrationRunner(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VATDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
