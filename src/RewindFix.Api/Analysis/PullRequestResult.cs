namespace RewindFix.Api.Analysis;

public sealed record PullRequestResult
{
    public required bool Success { get; init; }

    public string? Url { get; init; }

    public int? Number { get; init; }

    public string? Error { get; init; }
}