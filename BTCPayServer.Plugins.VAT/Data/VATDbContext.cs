using BTCPayServer.Plugins.VAT.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace BTCPayServer.Plugins.VAT.Data;

public class VATDbContext : DbContext
{
    public VATDbContext(DbContextOptions<VATDbContext> options) : base(options)
    {
    }

    public DbSet<VATStoreSettings> VATStoreSettings => Set<VATStoreSettings>();
    public DbSet<VATInvoiceRecord> VATInvoiceRecords => Set<VATInvoiceRecord>();
    public DbSet<VATEvidence> VATEvidence => Set<VATEvidence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("BTCPayServer.Plugins.VAT");

        modelBuilder.Entity<VATStoreSettings>(entity =>
        {
            entity.ToTable("VATStoreSettings");
            entity.HasIndex(e => e.StoreId).IsUnique();
        });

        modelBuilder.Entity<VATInvoiceRecord>(entity =>
        {
            entity.ToTable("VATInvoiceRecords");
            entity.HasIndex(e => e.StoreId);
            entity.HasIndex(e => e.CustomerCountry);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.StoreId, e.CreatedAt });
        });

        modelBuilder.Entity<VATEvidence>(entity =>
        {
            entity.ToTable("VATEvidence");
            entity.HasIndex(e => e.VATInvoiceRecordId);

            entity.HasOne(e => e.VATInvoiceRecord)
                .WithMany(r => r.Evidence)
                .HasForeignKey(e => e.VATInvoiceRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
