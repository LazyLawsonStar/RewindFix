var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/api/crashes", (CrashReportRequest crash) =>
{
    Console.WriteLine();
    Console.WriteLine("==========================================");
    Console.WriteLine("        REWINDFIX CRASH RECEIVED");
    Console.WriteLine("==========================================");
    Console.WriteLine($"Crash ID:  {crash.CrashId}");
    Console.WriteLine($"Time:      {crash.Timestamp}");
    Console.WriteLine($"Request:   {crash.Request.Method} {crash.Request.Path}");
    Console.WriteLine($"Exception: {crash.Exception.Type}");
    Console.WriteLine($"Message:   {crash.Exception.Message}");
    Console.WriteLine($"Trace ID:  {crash.TraceId}");
    Console.WriteLine();
    Console.WriteLine("Stack trace:");
    Console.WriteLine(crash.Exception.StackTrace);
    Console.WriteLine("==========================================");
    Console.WriteLine();

    return Results.Accepted(
        $"/api/crashes/{crash.CrashId}",
        new
        {
            crash.CrashId,
            Status = "received"
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