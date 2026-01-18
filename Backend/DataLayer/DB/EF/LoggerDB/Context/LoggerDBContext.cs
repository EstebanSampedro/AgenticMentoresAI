using Microsoft.EntityFrameworkCore;
using Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.LoggerDB.Entity;
using Microsoft.Extensions.Logging;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.LoggerDB.Context;

public partial class LoggerDBContext : DbContext
{
    private readonly string _connectionString;
    public LoggerDBContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    public LoggerDBContext(DbContextOptions<LoggerDBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<LogItem> LogItems { get; set; }

    /// <summary>
    /// protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //{
    //    if (!optionsBuilder.IsConfigured)
    //    {
    //        optionsBuilder.UseSqlServer(_connectionString.LoggerDBEntities);
    //    }
    //}
    /// </summary>
    /// <param name="optionsBuilder"></param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(_connectionString);
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Warning);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogItem>(entity =>
        {
            entity.HasKey(e => e.LogItemId)
                .HasName("PK__LogItem__B3647E735376667D")
                .IsClustered(false);

            entity.ToTable("LogItem");

            entity.HasIndex(e => e.LogItemDate, "IX_LogItem_LogItemDate").IsClustered();

            entity.Property(e => e.LogItemDate).HasColumnType("datetime");
            entity.Property(e => e.LogItemException)
                .HasMaxLength(8000)
                .IsUnicode(false);
            entity.Property(e => e.LogItemLevel)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.LogItemLogger)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.LogItemMessage)
                .HasMaxLength(8000)
                .IsUnicode(false);
            entity.Property(e => e.LogItemSource)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.LogItemThread)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
