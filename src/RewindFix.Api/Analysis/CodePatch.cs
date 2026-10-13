namespace RewindFix.Api.Analysis;

public sealed record CodePatch
{
    public required string FilePath { get; init; }

    public required int TargetLine { get; init; }

    public required string OriginalCode { get; init; }

    public required string ReplacementCode { get; init; }

    public required string Confidence { get; init; }
}