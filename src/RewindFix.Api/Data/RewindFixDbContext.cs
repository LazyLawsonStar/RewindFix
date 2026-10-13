using Microsoft.EntityFrameworkCore;
using RewindFix.Api.Entities;

namespace RewindFix.Api.Data;

public sealed class RewindFixDbContext : DbContext
{
    public RewindFixDbContext(
        DbContextOptions<RewindFixDbContext> options)
        : base(options)
    {
    }

    public DbSet<Crash> Crashes => Set<Crash>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var crash = modelBuilder.Entity<Crash>();

        crash.HasKey(x => x.Id);

        crash.Property(x => x.HttpMethod)
            .HasMaxLength(16);

        crash.Property(x => x.Path)
            .HasMaxLength(2048);

        crash.Property(x => x.ExceptionType)
            .HasMaxLength(512);

        crash.Property(x => x.TraceId)
            .HasMaxLength(256);

        crash.Property(x => x.ExceptionMessage)
            .HasColumnType("text");

        crash.Property(x => x.StackTrace)
            .HasColumnType("longtext");

        crash.Property(x => x.SourceFilePath)
            .HasMaxLength(4096);

        crash.Property(x => x.SourceFileName)
            .HasMaxLength(512);

        crash.Property(x => x.SourceMethod)
            .HasMaxLength(2048);
    }
}