using ColorVision.PluginMarketplace.Models;
using Microsoft.EntityFrameworkCore;

namespace ColorVision.PluginMarketplace.Data
{
    public class MarketplaceDbContext : DbContext
    {
        public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
            : base(options)
        {
        }

        public DbSet<Plugin> Plugins => Set<Plugin>();
        public DbSet<PluginVersion> PluginVersions => Set<PluginVersion>();
        public DbSet<DownloadRecord> DownloadRecords => Set<DownloadRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Plugin>(entity =>
            {
                entity.HasIndex(e => e.PluginId).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsPublished);
            });

            modelBuilder.Entity<PluginVersion>(entity =>
            {
                entity.HasIndex(e => new { e.PluginId, e.Version }).IsUnique();
                entity.HasOne(e => e.Plugin)
                      .WithMany(p => p.Versions)
                      .HasForeignKey(e => e.PluginId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DownloadRecord>(entity =>
            {
                entity.HasIndex(e => e.DownloadedAt);
                entity.HasOne(e => e.PluginVersion)
                      .WithMany()
                      .HasForeignKey(e => e.PluginVersionId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
