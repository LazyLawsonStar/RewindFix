using System.Diagnostics;
using Microsoft.Extensions.Options;
using RewindFix.Api.Configuration;

namespace RewindFix.Api.Analysis;

public sealed class GitPatchPublisher
{
    private readonly RewindFixOptions _rewindFixOptions;
    private readonly GitHubOptions _gitHubOptions;

    public GitPatchPublisher(
        IOptions<RewindFixOptions> rewindFixOptions,
        IOptions<GitHubOptions> gitHubOptions)
    {
        _rewindFixOptions = rewindFixOptions.Value;
        _gitHubOptions = gitHubOptions.Value;
    }

    public async Task<GitWorkspaceResult> CreateWorkspaceAsync(
        Guid crashId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(
                _rewindFixOptions.RepositoryRoot))
        {
            return GitWorkspaceResult.Fail(
                "RepositoryRoot is not configured.");
        }

        var repositoryRoot = Path.GetFullPath(
            _rewindFixOptions.RepositoryRoot);

        if (!Directory.Exists(repositoryRoot))
        {
            return GitWorkspaceResult.Fail(
                "Configured repository root does not exist.");
        }

        if (string.IsNullOrWhiteSpace(
                _gitHubOptions.BaseBranch))
        {
            return GitWorkspaceResult.Fail(
                "GitHub BaseBranch is not configured.");
        }

        var branchName =
            $"rewindfix/{crashId:N}-{Guid.NewGuid():N}";

        var workspacePath = Path.Combine(
            Path.GetTempPath(),
            "RewindFix",
            "git",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            Path.GetDirectoryName(workspacePath)!);

        var fetch = await RunGitAsync(
            repositoryRoot,
            cancellationToken,
            "fetch",
            "origin",
            _gitHubOptions.BaseBranch);

        if (!fetch.Success)
        {
            return GitWorkspaceResult.Fail(
                $"Git fetch failed: {fetch.Error}");
        }

        var create = await RunGitAsync(
            repositoryRoot,
            cancellationToken,
            "worktree",
            "add",
            "-b",
            branchName,
            workspacePath,
            $"origin/{_gitHubOptions.BaseBranch}");

        if (!create.Success)
        {
            return GitWorkspaceResult.Fail(
                $"Unable to create worktree: {create.Error}");
        }

        return new GitWorkspaceResult
        {
            Success = true,
            BranchName = branchName,
            WorkspacePath = workspacePath,
            RepositoryRoot = repositoryRoot
        };
    }

    public async Task<GitPatchResult> PublishAsync(
        GitWorkspaceResult workspace,
        string relativeFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!workspace.Success ||
            string.IsNullOrWhiteSpace(workspace.WorkspacePath) ||
            string.IsNullOrWhiteSpace(workspace.BranchName))
        {
            return new GitPatchResult
            {
                Success = false,
                Error = "Git workspace is invalid."
            };
        }

        var add = await RunGitAsync(
            workspace.WorkspacePath,
            cancellationToken,
            "add",
            "--",
            relativeFilePath);

        if (!add.Success)
        {
            return Fail(workspace, add.Error);
        }

        var commit = await RunGitAsync(
            workspace.WorkspacePath,
            cancellationToken,
            "commit",
            "-m",
            "Apply RewindFix generated patch");

        if (!commit.Success)
        {
            return Fail(workspace, commit.Error);
        }

        var sha = await RunGitAsync(
            workspace.WorkspacePath,
            cancellationToken,
            "rev-parse",
            "HEAD");

        if (!sha.Success)
        {
            return Fail(workspace, sha.Error);
        }

        var push = await RunGitAsync(
            workspace.WorkspacePath,
            cancellationToken,
            "push",
            "-u",
            "origin",
            workspace.BranchName);

        if (!push.Success)
        {
            return new GitPatchResult
            {
                Success = false,
                BranchName = workspace.BranchName,
                CommitSha = sha.Output.Trim(),
                WorkspacePath = workspace.WorkspacePath,
                Error = push.Error
            };
        }

        return new GitPatchResult
        {
            Success = true,
            BranchName = workspace.BranchName,
            CommitSha = sha.Output.Trim(),
            WorkspacePath = workspace.WorkspacePath
        };
    }

    public async Task CleanupAsync(
        GitWorkspaceResult workspace)
    {
        if (string.IsNullOrWhiteSpace(
                workspace.RepositoryRoot) ||
            string.IsNullOrWhiteSpace(
                workspace.WorkspacePath))
        {
            return;
        }

        await RunGitAsync(
            workspace.RepositoryRoot,
            CancellationToken.None,
            "worktree",
            "remove",
            "--force",
            workspace.WorkspacePath);

        await RunGitAsync(
            workspace.RepositoryRoot,
            CancellationToken.None,
            "worktree",
            "prune");
    }

    private static GitPatchResult Fail(
        GitWorkspaceResult workspace,
        string error)
    {
        return new GitPatchResult
        {
            Success = false,
            BranchName = workspace.BranchName,
            WorkspacePath = workspace.WorkspacePath,
            Error = error
        };
    }

    private static async Task<GitCommandResult> RunGitAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new GitCommandResult
        {
            Success = process.ExitCode == 0,
            Output = await outputTask,
            Error = await errorTask
        };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(
                    entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private sealed record GitCommandResult
    {
        public required bool Success { get; init; }

        public required string Output { get; init; }

        public required string Error { get; init; }
    }
}