namespace RewindFix.Api.Entities;

public sealed class Crash
{
    public Guid Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public required string HttpMethod { get; set; }

    public required string Path { get; set; }

    public string? QueryString { get; set; }

    public required string ExceptionType { get; set; }

    public required string ExceptionMessage { get; set; }

    public string? StackTrace { get; set; }

    public string? SourceFilePath { get; set; }

    public string? SourceFileName { get; set; }

    public int? SourceLine { get; set; }

    public string? SourceMethod { get; set; }

    public required string TraceId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}