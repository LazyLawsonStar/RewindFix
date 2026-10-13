using System.Diagnostics;

namespace RewindFix.Api.Analysis;

public sealed class GitPatchPublisher
{
    public async Task<GitPatchResult> PrepareAsync(
        CodePatch patch)
    {
        var repositoryRoot = FindRepositoryRoot(
            Path.GetDirectoryName(patch.FilePath));

        if (repositoryRoot is null)
        {
            return new GitPatchResult
            {
                Success = false,
                Error = "Unable to find Git repository root."
            };
        }

        var branchName =
            $"rewindfix/{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        var workspacePath = Path.Combine(
            Path.GetTempPath(),
            "RewindFix",
            "git",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            Path.GetDirectoryName(workspacePath)!);

        var worktreeResult = await RunGitAsync(
            repositoryRoot,
            $"worktree add -b \"{branchName}\" \"{workspacePath}\"");

        if (!worktreeResult.Success)
        {
            return new GitPatchResult
            {
                Success = false,
                BranchName = branchName,
                WorkspacePath = workspacePath,
                Error = worktreeResult.Error
            };
        }

        try
        {
            var relativeFilePath = Path.GetRelativePath(
                repositoryRoot,
                patch.FilePath);

            var workspaceFilePath = Path.Combine(
                workspacePath,
                relativeFilePath);

            await ApplyPatchAsync(
                workspaceFilePath,
                patch);

            var addResult = await RunGitAsync(
                workspacePath,
                $"add \"{relativeFilePath}\"");

            if (!addResult.Success)
            {
                return new GitPatchResult
                {
                    Success = false,
                    BranchName = branchName,
                    WorkspacePath = workspacePath,
                    Error = addResult.Error
                };
            }

            var commitResult = await RunGitAsync(
                workspacePath,
                "commit -m \"Apply RewindFix generated patch\"");

            if (!commitResult.Success)
            {
                return new GitPatchResult
                {
                    Success = false,
                    BranchName = branchName,
                    WorkspacePath = workspacePath,
                    Error = commitResult.Error
                };
            }

            var shaResult = await RunGitAsync(
                workspacePath,
                "rev-parse HEAD");

            if (!shaResult.Success)
            {
                return new GitPatchResult
                {
                    Success = false,
                    BranchName = branchName,
                    WorkspacePath = workspacePath,
                    Error = shaResult.Error
                };
            }

            var pushResult = await RunGitAsync(
                workspacePath,
                $"push -u origin \"{branchName}\"");

            if (!pushResult.Success)
            {
                return new GitPatchResult
                {
                    Success = false,
                    BranchName = branchName,
                    CommitSha = shaResult.Output.Trim(),
                    WorkspacePath = workspacePath,
                    Error = pushResult.Error
                };
            }

            return new GitPatchResult
            {
                Success = true,
                BranchName = branchName,
                CommitSha = shaResult.Output.Trim(),
                WorkspacePath = workspacePath
            };
        }
        catch (Exception exception)
        {
            return new GitPatchResult
            {
                Success = false,
                BranchName = branchName,
                WorkspacePath = workspacePath,
                Error = exception.Message
            };
        }
    }

    private static async Task ApplyPatchAsync(
        string filePath,
        CodePatch patch)
    {
        var lines = await File.ReadAllLinesAsync(filePath);

        var targetIndex = patch.TargetLine - 1;

        if (targetIndex < 0 ||
            targetIndex >= lines.Length)
        {
            throw new InvalidOperationException(
                "Patch target line is invalid.");
        }

        if (!string.Equals(
                lines[targetIndex].Trim(),
                patch.OriginalCode.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Patch target no longer matches original code.");
        }

        var replacementLines = patch.ReplacementCode
            .Replace("\r\n", "\n")
            .Split('\n');

        var indentation =
            GetIndentation(lines[targetIndex]);

        var result = new List<string>();

        result.AddRange(lines[..targetIndex]);

        foreach (var replacementLine in replacementLines)
        {
            if (string.IsNullOrWhiteSpace(replacementLine))
            {
                result.Add(string.Empty);
            }
            else
            {
                result.Add(indentation + replacementLine);
            }
        }

        result.AddRange(lines[(targetIndex + 1)..]);

        await File.WriteAllLinesAsync(
            filePath,
            result);
    }

    private static string? FindRepositoryRoot(
        string? directory)
    {
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (Directory.Exists(
                    Path.Combine(directory, ".git")))
            {
                return directory;
            }

            directory = Directory
                .GetParent(directory)?
                .FullName;
        }

        return null;
    }

    private static async Task<GitCommandResult> RunGitAsync(
        string workingDirectory,
        string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process
        {
            StartInfo = startInfo
        };

        process.Start();

        var outputTask =
            process.StandardOutput.ReadToEndAsync();

        var errorTask =
            process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        return new GitCommandResult
        {
            Success = process.ExitCode == 0,
            Output = output,
            Error = error
        };
    }

    private static string GetIndentation(
        string line)
    {
        var length =
            line.Length -
            line.TrimStart().Length;

        return line[..length];
    }

    private sealed record GitCommandResult
    {
        public required bool Success { get; init; }

        public required string Output { get; init; }

        public required string Error { get; init; }
    }
}