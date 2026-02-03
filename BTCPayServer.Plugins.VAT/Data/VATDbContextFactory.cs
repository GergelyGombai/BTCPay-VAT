using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BTCPayServer.Plugins.VAT.Data;

public class VATDbContextFactory : IDesignTimeDbContextFactory<VATDbContext>
{
    public VATDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<VATDbContext>();

        // Use a placeholder connection string for design-time migrations
        optionsBuilder.UseNpgsql("Host=localhost;Database=btcpay;Username=postgres;Password=postgres");

        return new VATDbContext(optionsBuilder.Options);
    }
}
