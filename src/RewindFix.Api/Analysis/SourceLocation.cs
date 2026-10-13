namespace RewindFix.Api.Analysis;

public sealed record SourceLocation
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public required int Line { get; init; }

    public string? Method { get; init; }
}