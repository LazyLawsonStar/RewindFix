using System.Diagnostics;

namespace RewindFix.Api.Analysis;

public sealed class PatchValidator
{
    public async Task<PatchValidationResult> ValidateAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workspacePath))
        {
            return Fail(
                "Validation workspace does not exist.");
        }

        var target = FindBuildTarget(workspacePath);

        if (target is null)
        {
            return Fail(
                "No solution or project was found.");
        }

        var restore = await RunDotNetAsync(
            workspacePath,
            cancellationToken,
            "restore",
            target);

        if (!restore.Success)
        {
            return new PatchValidationResult
            {
                Success = false,
                ExitCode = restore.ExitCode,
                Output = restore.Output,
                ErrorOutput =
                    "dotnet restore failed." +
                    Environment.NewLine +
                    restore.Error,
                WorkspacePath = workspacePath
            };
        }

        var build = await RunDotNetAsync(
            workspacePath,
            cancellationToken,
            "build",
            target,
            "--no-restore");

        return new PatchValidationResult
        {
            Success = build.Success,
            ExitCode = build.ExitCode,
            Output =
                restore.Output +
                Environment.NewLine +
                build.Output,
            ErrorOutput =
                restore.Error +
                Environment.NewLine +
                build.Error,
            WorkspacePath = workspacePath
        };
    }

    private static string? FindBuildTarget(
        string workspacePath)
    {
        var solution = Directory
            .EnumerateFiles(
                workspacePath,
                "*.sln",
                SearchOption.TopDirectoryOnly)
            .Concat(
                Directory.EnumerateFiles(
                    workspacePath,
                    "*.slnx",
                    SearchOption.TopDirectoryOnly))
            .FirstOrDefault();

        if (solution is not null)
        {
            return solution;
        }

        return Directory
            .EnumerateFiles(
                workspacePath,
                "*.csproj",
                SearchOption.AllDirectories)
            .FirstOrDefault(IsUsableProject);
    }

    private static bool IsUsableProject(
        string path)
    {
        return !ContainsDirectory(path, "bin") &&
               !ContainsDirectory(path, "obj");
    }

    private static bool ContainsDirectory(
        string path,
        string directory)
    {
        var marker =
            $"{Path.DirectorySeparatorChar}" +
            directory +
            $"{Path.DirectorySeparatorChar}";

        return path.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ProcessResult> RunDotNetAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
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

        return new ProcessResult(
            process.ExitCode == 0,
            process.ExitCode,
            await outputTask,
            await errorTask);
    }

    private static PatchValidationResult Fail(
        string error)
    {
        return new PatchValidationResult
        {
            Success = false,
            ExitCode = -1,
            Output = string.Empty,
            ErrorOutput = error
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
        }
    }

    private sealed record ProcessResult(
        bool Success,
        int ExitCode,
        string Output,
        string Error);
}