using Microsoft.EntityFrameworkCore;
using RewindFix.Api.Data;
using RewindFix.Api.Entities;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("RewindFix")
    ?? throw new InvalidOperationException(
        "Connection string 'RewindFix' was not found.");

builder.Services.AddDbContext<RewindFixDbContext>(options =>
{
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/api/crashes", async (
    CrashReportRequest report,
    RewindFixDbContext db) =>
{
    var exists = await db.Crashes
        .AnyAsync(x => x.Id == report.CrashId);

    if (exists)
    {
        return Results.Ok(new
        {
            CrashId = report.CrashId,
            Status = "already_received"
        });
    }

    var crash = new Crash
    {
        Id = report.CrashId,
        Timestamp = report.Timestamp,

        HttpMethod = report.Request.Method,
        Path = report.Request.Path,
        QueryString = report.Request.QueryString,

        ExceptionType = report.Exception.Type,
        ExceptionMessage = report.Exception.Message,
        StackTrace = report.Exception.StackTrace,

        TraceId = report.TraceId,
        ReceivedAt = DateTimeOffset.UtcNow
    };

    db.Crashes.Add(crash);

    await db.SaveChangesAsync();

    Console.WriteLine();
    Console.WriteLine("==========================================");
    Console.WriteLine("        REWINDFIX CRASH STORED");
    Console.WriteLine("==========================================");
    Console.WriteLine($"Crash ID:  {crash.Id}");
    Console.WriteLine($"Request:   {crash.HttpMethod} {crash.Path}");
    Console.WriteLine($"Exception: {crash.ExceptionType}");
    Console.WriteLine($"Message:   {crash.ExceptionMessage}");
    Console.WriteLine("==========================================");
    Console.WriteLine();

    return Results.Created(
        $"/api/crashes/{crash.Id}",
        new
        {
            CrashId = crash.Id,
            Status = "stored"
        });
});

app.MapGet("/api/crashes", async (
    RewindFixDbContext db) =>
{
    var crashes = await db.Crashes
        .AsNoTracking()
        .OrderByDescending(x => x.ReceivedAt)
        .Select(x => new
        {
            CrashId = x.Id,
            x.Timestamp,
            Method = x.HttpMethod,
            x.Path,
            Exception = x.ExceptionType,
            Message = x.ExceptionMessage,
            x.ReceivedAt
        })
        .ToListAsync();

    return Results.Ok(crashes);
});

app.MapGet("/api/crashes/{id:guid}", async (
    Guid id,
    RewindFixDbContext db) =>
{
    var crash = await db.Crashes
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    return crash is null
        ? Results.NotFound()
        : Results.Ok(crash);
});

app.Run();

public sealed record CrashReportRequest
{
    public required Guid CrashId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required CrashRequestData Request { get; init; }

    public required CrashExceptionData Exception { get; init; }

    public required string TraceId { get; init; }
}

public sealed record CrashRequestData
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    public string? QueryString { get; init; }
}

public sealed record CrashExceptionData
{
    public required string Type { get; init; }

    public required string Message { get; init; }

    public string? StackTrace { get; init; }
}