using Microsoft.EntityFrameworkCore;
using RewindFix.Api.Data;
using RewindFix.Api.Entities;
using RewindFix.Api.Analysis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<StackTraceParser>();
builder.Services.AddSingleton<SourceContextReader>();
builder.Services.AddSingleton<RootCauseAnalyzer>();

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
    RewindFixDbContext db,
    StackTraceParser parser) =>
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

    var sourceLocation = parser.Parse(report.Exception.StackTrace);

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

        SourceFilePath = sourceLocation?.FilePath,
        SourceFileName = sourceLocation?.FileName,
        SourceLine = sourceLocation?.Line,
        SourceMethod = sourceLocation?.Method,

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

app.MapGet("/api/crashes/{id:guid}/analysis", async (
    Guid id,
    RewindFixDbContext db,
    SourceContextReader sourceReader,
    RootCauseAnalyzer rootCauseAnalyzer) =>
{
    var crash = await db.Crashes
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (crash is null)
    {
        return Results.NotFound();
    }

    var sourceContext = await sourceReader.ReadAsync(
        crash.SourceFilePath,
        crash.SourceLine);

    var rootCause = rootCauseAnalyzer.Analyze(
        crash.ExceptionType,
        sourceContext);

    return Results.Ok(new
    {
        CrashId = crash.Id,

        Exception = new
        {
            Type = crash.ExceptionType,
            Message = crash.ExceptionMessage
        },

        SourceLocation = new
        {
            FilePath = crash.SourceFilePath,
            FileName = crash.SourceFileName,
            Line = crash.SourceLine,
            Method = crash.SourceMethod
        },

        SourceContext = sourceContext,

        RootCause = rootCause
    });
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