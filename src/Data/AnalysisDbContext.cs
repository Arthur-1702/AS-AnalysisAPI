using AnalysisService.Models;
using Microsoft.EntityFrameworkCore;

namespace AnalysisService.Data;

public class AnalysisDbContext(DbContextOptions<AnalysisDbContext> options) : DbContext(options)
{
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<FieldStatus> FieldStatuses => Set<FieldStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Alert>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.FieldId);
            e.HasIndex(a => new { a.FieldId, a.IsActive });
            e.HasIndex(a => a.TriggeredAt);
            e.Property(a => a.Message).HasMaxLength(500).IsRequired();
            e.Property(a => a.Type).HasConversion<string>();
            e.Property(a => a.Severity).HasConversion<string>();
        });

        modelBuilder.Entity<FieldStatus>(e =>
        {
            e.HasKey(fs => fs.FieldId);   // FieldId é a própria PK (1 status por talhão)
            e.Property(fs => fs.Status).HasConversion<string>();
        });
    }
}
