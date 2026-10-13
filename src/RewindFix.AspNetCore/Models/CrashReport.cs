namespace RewindFix.AspNetCore.Models;

public sealed record CrashReport
{
    public required Guid CrashId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required CrashRequest Request { get; init; }

    public required CrashException Exception { get; init; }

    public required string TraceId { get; init; }
}

public sealed record CrashRequest
{
    public required string Method { get; init; }

    public required string Path { get; init; }

    public string? QueryString { get; init; }
}

public sealed record CrashException
{
    public required string Type { get; init; }

    public required string Message { get; init; }

    public string? StackTrace { get; init; }
}