namespace RewindFix.Api.Analysis;

public sealed record RootCauseAnalysis
{
    public required string Summary { get; init; }

    public required string Confidence { get; init; }

    public string? VariableName { get; init; }

    public string? SuggestedAction { get; init; }
}