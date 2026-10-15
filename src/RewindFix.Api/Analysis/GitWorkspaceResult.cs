namespace RewindFix.Api.Analysis;

public sealed record GitWorkspaceResult
{
    public required bool Success { get; init; }

    public string? BranchName { get; init; }

    public string? WorkspacePath { get; init; }

    public string? RepositoryRoot { get; init; }

    public string? Error { get; init; }

    public static GitWorkspaceResult Fail(
        string error)
    {
        return new GitWorkspaceResult
        {
            Success = false,
            Error = error
        };
    }
}