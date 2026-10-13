namespace RewindFix.Api.Analysis;

public sealed record TestValidationResult
{
    public required bool Success { get; init; }

    public required bool TestsFound { get; init; }

    public required int ExitCode { get; init; }

    public required string Output { get; init; }

    public required string ErrorOutput { get; init; }
}