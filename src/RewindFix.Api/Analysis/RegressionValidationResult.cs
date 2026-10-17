namespace RewindFix.Api.Analysis;

public sealed record RegressionValidationResult
{
    public required bool Success { get; init; }

    public required bool Reproduced { get; init; }

    public required int? StatusCode { get; init; }

    public required string Output { get; init; }

    public required string ErrorOutput { get; init; }
}