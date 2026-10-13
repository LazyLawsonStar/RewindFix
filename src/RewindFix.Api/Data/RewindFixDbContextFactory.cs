using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RewindFix.Api.Data;

public sealed class RewindFixDbContextFactory
    : IDesignTimeDbContextFactory<RewindFixDbContext>
{
    public RewindFixDbContext CreateDbContext(string[] args)
    {
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile(
                $"appsettings.{environment}.json",
                optional: true)
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration
            .GetConnectionString("RewindFix")
            ?? throw new InvalidOperationException(
                "Connection string 'RewindFix' was not found.");

        var optionsBuilder =
            new DbContextOptionsBuilder<RewindFixDbContext>();

        optionsBuilder.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString));

        return new RewindFixDbContext(optionsBuilder.Options);
    }
}