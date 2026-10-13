namespace RewindFix.Api.Analysis;

public sealed record PatchApplyResult
{
    public required bool Success { get; init; }

    public required string OutputFilePath { get; init; }

    public string? Error { get; init; }
}