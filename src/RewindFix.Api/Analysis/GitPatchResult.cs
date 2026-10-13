namespace RewindFix.Api.Analysis;

public sealed record GitPatchResult
{
    public required bool Success { get; init; }

    public string? BranchName { get; init; }

    public string? CommitSha { get; init; }

    public string? WorkspacePath { get; init; }

    public string? Error { get; init; }
}