using Microsoft.EntityFrameworkCore;
using RewindFix.Api.Data;
using RewindFix.Api.Entities;
using RewindFix.Api.Analysis;
using RewindFix.Api.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RewindFixOptions>(
    builder.Configuration.GetSection("RewindFix"));

builder.Services.AddSingleton<StackTraceParser>();
builder.Services.AddSingleton<SourceContextReader>();
builder.Services.AddSingleton<RootCauseAnalyzer>();
builder.Services.AddSingleton<SuggestedFixGenerator>();
builder.Services.AddSingleton<PatchGenerator>();
builder.Services.AddSingleton<PatchApplier>();
builder.Services.AddSingleton<PatchValidator>();
builder.Services.AddSingleton<TestValidator>();
builder.Services.AddSingleton<GitPatchPublisher>();

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
    RootCauseAnalyzer rootCauseAnalyzer,
    SuggestedFixGenerator fixGenerator,
    PatchGenerator patchGenerator) =>
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

    var suggestedFix = fixGenerator.Generate(
        rootCause,
        sourceContext);

    var patch = patchGenerator.Generate(
        sourceContext,
        suggestedFix);

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

        RootCause = rootCause,

        SuggestedFix = suggestedFix,

        Patch = patch
    });
});

app.MapPost("/api/crashes/{id:guid}/apply-preview", async (
    Guid id,
    RewindFixDbContext db,
    SourceContextReader sourceReader,
    RootCauseAnalyzer rootCauseAnalyzer,
    SuggestedFixGenerator fixGenerator,
    PatchGenerator patchGenerator,
    IOptions<RewindFixOptions> options) =>
{
    if (options.Value.AutomationLevel < AutomationLevel.Patch)
    {
        return Results.StatusCode(
            StatusCodes.Status403Forbidden);
    }

    var crash = await db.Crashes
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (crash is null)
    {
        return Results.NotFound();
    }

    var sourceContext =
        await sourceReader.ReadAsync(
            crash.SourceFilePath,
            crash.SourceLine);

    var rootCause =
        rootCauseAnalyzer.Analyze(
            crash.ExceptionType,
            sourceContext);

    var suggestedFix =
        fixGenerator.Generate(
            rootCause,
            sourceContext);

    var patch =
        patchGenerator.Generate(
            sourceContext,
            suggestedFix);

    if (patch is null)
    {
        return Results.BadRequest(new
        {
            Error = "Unable to generate patch."
        });
    }

    return Results.Ok(new
    {
        Status = "preview_ready",
        Patch = patch
    });
});

app.MapPost("/api/crashes/{id:guid}/validate-patch", async (
    Guid id,
    RewindFixDbContext db,
    SourceContextReader sourceReader,
    RootCauseAnalyzer rootCauseAnalyzer,
    SuggestedFixGenerator fixGenerator,
    PatchGenerator patchGenerator,
    PatchValidator patchValidator,
    TestValidator testValidator,
    IOptions<RewindFixOptions> options) =>
{
    if (options.Value.AutomationLevel < AutomationLevel.Validate)
    {
        return Results.StatusCode(
            StatusCodes.Status403Forbidden);
    }

    var crash = await db.Crashes
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (crash is null)
    {
        return Results.NotFound();
    }

    var sourceContext =
        await sourceReader.ReadAsync(
            crash.SourceFilePath,
            crash.SourceLine);

    var rootCause =
        rootCauseAnalyzer.Analyze(
            crash.ExceptionType,
            sourceContext);

    var suggestedFix =
        fixGenerator.Generate(
            rootCause,
            sourceContext);

    var patch =
        patchGenerator.Generate(
            sourceContext,
            suggestedFix);

    if (patch is null)
    {
        return Results.BadRequest(new
        {
            Error = "Unable to generate patch."
        });
    }

    var buildValidation =
        await patchValidator.ValidateAsync(patch);

    TestValidationResult? testValidation = null;

    if (buildValidation.Success &&
        !string.IsNullOrWhiteSpace(
            buildValidation.WorkspacePath))
    {
        testValidation =
            await testValidator.ValidateAsync(
                buildValidation.WorkspacePath);
    }

    return Results.Ok(new
    {
        Patch = patch,
        BuildValidation = buildValidation,
        TestValidation = testValidation
    });
});

app.MapPost("/api/crashes/{id:guid}/prepare-git-fix", async (
    Guid id,
    RewindFixDbContext db,
    SourceContextReader sourceReader,
    RootCauseAnalyzer rootCauseAnalyzer,
    SuggestedFixGenerator fixGenerator,
    PatchGenerator patchGenerator,
    PatchValidator patchValidator,
    TestValidator testValidator,
    GitPatchPublisher gitPatchPublisher,
    IOptions<RewindFixOptions> options) =>
{
    if (options.Value.AutomationLevel < AutomationLevel.Publish)
    {
        return Results.StatusCode(
            StatusCodes.Status403Forbidden);
    }

    var crash = await db.Crashes
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id);

    if (crash is null)
    {
        return Results.NotFound();
    }

    var sourceContext =
        await sourceReader.ReadAsync(
            crash.SourceFilePath,
            crash.SourceLine);

    var rootCause =
        rootCauseAnalyzer.Analyze(
            crash.ExceptionType,
            sourceContext);

    var suggestedFix =
        fixGenerator.Generate(
            rootCause,
            sourceContext);

    var patch =
        patchGenerator.Generate(
            sourceContext,
            suggestedFix);

    if (patch is null)
    {
        return Results.BadRequest(new
        {
            Error = "Unable to generate patch."
        });
    }

    var buildValidation =
        await patchValidator.ValidateAsync(patch);

    if (!buildValidation.Success ||
        string.IsNullOrWhiteSpace(
            buildValidation.WorkspacePath))
    {
        return Results.BadRequest(new
        {
            Error = "Generated patch did not pass build validation.",
            BuildValidation = buildValidation
        });
    }

    var testValidation =
        await testValidator.ValidateAsync(
            buildValidation.WorkspacePath);

    if (!testValidation.Success)
    {
        return Results.BadRequest(new
        {
            Error = "Generated patch did not pass tests.",
            BuildValidation = buildValidation,
            TestValidation = testValidation
        });
    }

    var gitResult =
        await gitPatchPublisher.PrepareAsync(patch);

    if (!gitResult.Success)
    {
        return Results.BadRequest(new
        {
            Error = "Unable to prepare Git fix.",
            BuildValidation = buildValidation,
            TestValidation = testValidation,
            Git = gitResult
        });
    }

    return Results.Ok(new
    {
        Patch = patch,
        BuildValidation = buildValidation,
        TestValidation = testValidation,
        Git = gitResult
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