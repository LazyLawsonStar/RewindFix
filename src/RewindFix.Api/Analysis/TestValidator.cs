using System.Diagnostics;

namespace RewindFix.Api.Analysis;

public sealed class TestValidator
{
    public async Task<TestValidationResult> ValidateAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
        var testProjects = Directory
            .EnumerateFiles(
                workspacePath,
                "*.csproj",
                SearchOption.AllDirectories)
            .Where(IsTestProject)
            .ToList();

        if (testProjects.Count == 0)
        {
            return new TestValidationResult
            {
                Success = true,
                TestsFound = false,
                ExitCode = 0,
                Output = "No test projects found.",
                ErrorOutput = string.Empty
            };
        }

        var output = new List<string>();
        var errors = new List<string>();

        foreach (var testProject in testProjects)
        {
            var result = await RunTestAsync(
                workspacePath,
                testProject,
                cancellationToken);

            output.Add(result.Output);
            errors.Add(result.Error);

            if (!result.Success)
            {
                return new TestValidationResult
                {
                    Success = false,
                    TestsFound = true,
                    ExitCode = result.ExitCode,
                    Output = string.Join(
                        Environment.NewLine,
                        output),
                    ErrorOutput = string.Join(
                        Environment.NewLine,
                        errors)
                };
            }
        }

        return new TestValidationResult
        {
            Success = true,
            TestsFound = true,
            ExitCode = 0,
            Output = string.Join(
                Environment.NewLine,
                output),
            ErrorOutput = string.Join(
                Environment.NewLine,
                errors)
        };
    }

    private static bool IsTestProject(
        string path)
    {
        if (ContainsDirectory(path, "bin") ||
            ContainsDirectory(path, "obj"))
        {
            return false;
        }

        var name =
            Path.GetFileNameWithoutExtension(path);

        return name.EndsWith(
                   ".Tests",
                   StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(
                   ".Test",
                   StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(
                   ".IntegrationTests",
                   StringComparison.OrdinalIgnoreCase);
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

    private static async Task<ProcessResult> RunTestAsync(
        string workingDirectory,
        string testProject,
        CancellationToken cancellationToken)
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

        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(testProject);

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