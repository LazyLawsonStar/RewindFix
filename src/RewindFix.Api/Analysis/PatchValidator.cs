using System.Diagnostics;

namespace RewindFix.Api.Analysis;

public sealed class PatchValidator
{
    public async Task<PatchValidationResult> ValidateAsync(
        CodePatch patch)
    {
        if (!File.Exists(patch.FilePath))
        {
            return new PatchValidationResult
            {
                Success = false,
                ExitCode = -1,
                Output = string.Empty,
                ErrorOutput = "Source file does not exist."
            };
        }

        var projectDirectory = FindProjectDirectory(
            Path.GetDirectoryName(patch.FilePath));

        if (projectDirectory is null)
        {
            return new PatchValidationResult
            {
                Success = false,
                ExitCode = -1,
                Output = string.Empty,
                ErrorOutput = "Unable to find project directory."
            };
        }

        var repositoryRoot = FindRepositoryRoot(projectDirectory);

        if (repositoryRoot is null)
        {
            return new PatchValidationResult
            {
                Success = false,
                ExitCode = -1,
                Output = string.Empty,
                ErrorOutput = "Unable to find repository root."
            };
        }

        var workspacePath = Path.Combine(
            Path.GetTempPath(),
            "RewindFix",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workspacePath);

        CopyDirectory(
            repositoryRoot,
            workspacePath);

        var relativeSourceFilePath = Path.GetRelativePath(
            repositoryRoot,
            patch.FilePath);

        var workspaceFilePath = Path.Combine(
            workspacePath,
            relativeSourceFilePath);

        await ApplyPatchAsync(
            workspaceFilePath,
            patch);

        var relativeProjectPath = Path.GetRelativePath(
            repositoryRoot,
            projectDirectory);

        var workspaceProjectDirectory = Path.Combine(
            workspacePath,
            relativeProjectPath);

        var projectFile = Directory
            .EnumerateFiles(
                workspaceProjectDirectory,
                "*.csproj")
            .FirstOrDefault();

        if (projectFile is null)
        {
            return new PatchValidationResult
            {
                Success = false,
                ExitCode = -1,
                Output = string.Empty,
                ErrorOutput = "Project file was not found in workspace.",
                WorkspacePath = workspacePath
            };
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{projectFile}\"",
            WorkingDirectory = workspacePath,
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
        var errorOutput = await errorTask;

        return new PatchValidationResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            Output = output,
            ErrorOutput = errorOutput,
            WorkspacePath = workspacePath
        };
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

        result.AddRange(
            lines[(targetIndex + 1)..]);

        await File.WriteAllLinesAsync(
            filePath,
            result);
    }

    private static string? FindProjectDirectory(
        string? directory)
    {
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (Directory
                .EnumerateFiles(directory, "*.csproj")
                .Any())
            {
                return directory;
            }

            directory = Directory
                .GetParent(directory)?
                .FullName;
        }

        return null;
    }

    private static string? FindRepositoryRoot(
        string directory)
    {
        var current = directory;

        while (!string.IsNullOrWhiteSpace(current))
        {
            var hasGitDirectory =
                Directory.Exists(
                    Path.Combine(current, ".git"));

            var hasSolution =
                Directory
                    .EnumerateFiles(current, "*.sln")
                    .Any() ||
                Directory
                    .EnumerateFiles(current, "*.slnx")
                    .Any();

            if (hasGitDirectory || hasSolution)
            {
                return current;
            }

            current = Directory
                .GetParent(current)?
                .FullName;
        }

        return null;
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        foreach (var directory in Directory
            .EnumerateDirectories(
                source,
                "*",
                SearchOption.AllDirectories))
        {
            if (ShouldSkipPath(directory))
            {
                continue;
            }

            var relative =
                Path.GetRelativePath(
                    source,
                    directory);

            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    relative));
        }

        foreach (var file in Directory
            .EnumerateFiles(
                source,
                "*",
                SearchOption.AllDirectories))
        {
            if (ShouldSkipPath(file))
            {
                continue;
            }

            if (Path.GetFileName(file)
                .Contains(".rewindfix."))
            {
                continue;
            }

            var relative =
                Path.GetRelativePath(
                    source,
                    file);

            var target =
                Path.Combine(
                    destination,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(target)!);

            File.Copy(
                file,
                target,
                overwrite: true);
        }
    }

    private static bool ShouldSkipPath(
        string path)
    {
        var separators = new[]
        {
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"
        };

        return separators.Any(
            separator =>
                path.Contains(
                    separator,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string GetIndentation(
        string line)
    {
        var length =
            line.Length -
            line.TrimStart().Length;

        return line[..length];
    }
}