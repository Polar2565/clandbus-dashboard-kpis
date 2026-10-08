using ClandbusERPIntegration.Models;
using Microsoft.EntityFrameworkCore;

namespace ClandbusERPIntegration.Data;

public sealed class DashboardDbContext(DbContextOptions<DashboardDbContext> options)
    : DbContext(options)
{
    public DbSet<CaseSnapshot> Cases => Set<CaseSnapshot>();
    public DbSet<TaskSnapshot> Tasks => Set<TaskSnapshot>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<ProfessionalRecord> ProfessionalRecords => Set<ProfessionalRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseSnapshot>()
            .HasIndex(x => new { x.UserKey, x.CaseNumber, x.CapturedAt });
        modelBuilder.Entity<TaskSnapshot>()
            .HasIndex(x => new { x.UserKey, x.ExternalId, x.CapturedAt });
        modelBuilder.Entity<SyncRun>().HasIndex(x => new { x.UserKey, x.FinishedAt });
        modelBuilder.Entity<ProfessionalRecord>().HasIndex(x => new { x.UserKey, x.Date });
    }
}
