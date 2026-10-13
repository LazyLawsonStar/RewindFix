namespace RewindFix.Api.Analysis;

public sealed record SuggestedFix
{
    public required string Summary { get; init; }

    public required string Confidence { get; init; }

    public string? ReplacementCode { get; init; }

    public string? Notes { get; init; }
}