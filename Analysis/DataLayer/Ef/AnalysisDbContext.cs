using Academikus.AnalysisMentoresVerdes.Entity.Analysis;
using Microsoft.EntityFrameworkCore;

public sealed class AnalysisDbContext : DbContext
{
    public AnalysisDbContext(DbContextOptions<AnalysisDbContext> o) : base(o) { }
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<AnalysisObservation> AnalysisObservations => Set<AnalysisObservation>();
    public DbSet<AnalysisParameterDefinition> AnalysisParameterDefinitions => Set<AnalysisParameterDefinition>();
    public DbSet<AnalysisSummary> AnalysisSummaries => Set<AnalysisSummary>();
    protected override void OnModelCreating(ModelBuilder b)
    { 
        // AnalysisRun -> dbo.AnalysisRun
        b.Entity<AnalysisRun>(e =>
        {
            e.ToTable("AnalysisRun", "dbo");
            e.HasKey(x => x.RunId);
            e.Property(x => x.RunId).ValueGeneratedOnAdd();
            e.Property(x => x.WeekStartUtc).HasColumnType("datetime2(0)").IsRequired();
            e.Property(x => x.WeekEndUtc).HasColumnType("datetime2(0)").IsRequired();
            e.Property(x => x.Status).HasMaxLength(50).IsRequired();
            e.Property(x => x.ModelVersion).HasMaxLength(50).IsRequired();
            e.Property(x => x.PromptVersion).HasMaxLength(50).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)").IsRequired();
        });

        // AnalysisSummary -> dbo.AnalysisSummary
        b.Entity<AnalysisSummary>(e =>
        {
            e.ToTable("AnalysisSummary", "dbo");
            e.HasKey(x => x.SummaryId);
            e.Property(x => x.SummaryId).ValueGeneratedOnAdd();
            e.Property(x => x.WeekStartUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.WeekEndUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.RawJson).HasColumnType("nvarchar(max)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
            e.HasOne<AnalysisRun>()
                .WithMany()
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AnalysisParameterDefinition -> dbo.AnalysisParameterDefinition
        b.Entity<AnalysisParameterDefinition>(e =>
        {
            e.ToTable("AnalysisParameterDefinition", "dbo");
            e.HasKey(x => x.ParameterId);
            e.Property(x => x.ParameterId).ValueGeneratedOnAdd();
            e.Property(x => x.MinScore).HasColumnType("decimal(5,2)").IsRequired(false);
            e.Property(x => x.MaxScore).HasColumnType("decimal(5,2)").IsRequired(false);
        });

        // AnalysisObservation -> dbo.AnalysisObservation
        b.Entity<AnalysisObservation>(e =>
        {
            e.ToTable("AnalysisObservation", "dbo");
            e.HasKey(x => x.ObservationId);
            e.Property(x => x.ObservationId).ValueGeneratedOnAdd();
            e.Property(x => x.ParameterScore).HasColumnType("decimal(5,2)").IsRequired(false);
            e.Property(x => x.ParameterContent).HasColumnType("nvarchar(max)").IsRequired(false);
            e.Property(x => x.WeekStartUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.WeekEndUtc).HasColumnType("datetime2(0)");
            e.Property(x => x.CreatedAt).HasColumnType("datetime2(0)");
            e.HasOne<AnalysisRun>()
                .WithMany()
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AnalysisParameterDefinition>()
                .WithMany()
                .HasForeignKey(x => x.ParameterId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
