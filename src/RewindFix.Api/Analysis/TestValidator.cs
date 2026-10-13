using System.Diagnostics;

namespace RewindFix.Api.Analysis;

public sealed class TestValidator
{
    public async Task<TestValidationResult> ValidateAsync(
        string workspacePath)
    {
        var testProjects = Directory
            .EnumerateFiles(
                workspacePath,
                "*.csproj",
                SearchOption.AllDirectories)
            .Where(path =>
                path.Contains(
                    "test",
                    StringComparison.OrdinalIgnoreCase))
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

        var combinedOutput = new List<string>();
        var combinedErrors = new List<string>();

        foreach (var testProject in testProjects)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{testProject}\" --no-restore",
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

            combinedOutput.Add(output);
            combinedErrors.Add(errorOutput);

            if (process.ExitCode != 0)
            {
                return new TestValidationResult
                {
                    Success = false,
                    TestsFound = true,
                    ExitCode = process.ExitCode,
                    Output = string.Join(
                        Environment.NewLine,
                        combinedOutput),
                    ErrorOutput = string.Join(
                        Environment.NewLine,
                        combinedErrors)
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
                combinedOutput),
            ErrorOutput = string.Join(
                Environment.NewLine,
                combinedErrors)
        };
    }
}