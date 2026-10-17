using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using RewindFix.Api.Configuration;
using RewindFix.Api.Entities;

namespace RewindFix.Api.Analysis;

public sealed class RegressionValidator
{
    private static readonly TimeSpan StartupTimeout =
        TimeSpan.FromSeconds(30);

    private static readonly TimeSpan RequestTimeout =
        TimeSpan.FromSeconds(10);

    private readonly RewindFixOptions _options;

    public RegressionValidator(
        IOptions<RewindFixOptions> options)
    {
        _options = options.Value;
    }

    public async Task<RegressionValidationResult> ValidateAsync(
        string workspacePath,
        Crash crash,
        bool expectCrash,
        CancellationToken cancellationToken = default)
    {
        var configuration =
            ValidateConfiguration(workspacePath);

        if (!configuration.Success)
        {
            return configuration;
        }

        var workspaceRoot =
            Path.GetFullPath(workspacePath);

        var projectPath =
            Path.GetFullPath(
                Path.Combine(
                    workspaceRoot,
                    _options.RegressionProject!));

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(baseUrl);

        // Prevent the reproduced crash from being sent back
        // to the real RewindFix server.
        startInfo.Environment["RewindFix__ServerUrl"] =
            "http://127.0.0.1:1";

        using var process =
            new Process
            {
                StartInfo = startInfo
            };

        try
        {
            process.Start();

            var outputTask =
                process.StandardOutput.ReadToEndAsync();

            var errorTask =
                process.StandardError.ReadToEndAsync();

            using var client =
                new HttpClient
                {
                    Timeout = RequestTimeout
                };

            var ready =
                await WaitUntilReadyAsync(
                    client,
                    baseUrl,
                    process,
                    cancellationToken);

            if (!ready)
            {
                TryKill(process);

                return new RegressionValidationResult
                {
                    Success = false,
                    Reproduced = false,
                    StatusCode = null,
                    Output = await outputTask,
                    ErrorOutput =
                        "Regression application did not start " +
                        $"within {StartupTimeout.TotalSeconds:0} seconds." +
                        Environment.NewLine +
                        await errorTask
                };
            }

            var requestUri =
                BuildRequestUri(
                    baseUrl,
                    crash.Path,
                    crash.QueryString);

            using var request =
                new HttpRequestMessage(
                    new HttpMethod(crash.HttpMethod),
                    requestUri);

            using var response =
                await client.SendAsync(
                    request,
                    cancellationToken);

            var statusCode =
                (int)response.StatusCode;

            var reproduced =
                statusCode >= 500;

            var success =
                expectCrash
                    ? reproduced
                    : !reproduced;

            return new RegressionValidationResult
            {
                Success = success,
                Reproduced = reproduced,
                StatusCode = statusCode,
                Output =
                    $"Regression request returned HTTP {statusCode}.",
                ErrorOutput =
                    success
                        ? string.Empty
                        : expectCrash
                            ? "The original crash could not be reproduced."
                            : "The crash still reproduces after the patch."
            };
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new RegressionValidationResult
            {
                Success = false,
                Reproduced = false,
                StatusCode = null,
                Output = string.Empty,
                ErrorOutput = exception.Message
            };
        }
        finally
        {
            TryKill(process);
        }
    }

    public RegressionValidationResult ValidateConfiguration(
        string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return Fail(
                "Workspace path is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                _options.RegressionProject))
        {
            return Fail(
                "Regression project is not configured.");
        }

        var workspaceRoot =
            Path.GetFullPath(workspacePath);

        var projectPath =
            Path.GetFullPath(
                Path.Combine(
                    workspaceRoot,
                    _options.RegressionProject));

        if (!IsInsideRoot(
                workspaceRoot,
                projectPath))
        {
            return Fail(
                "Regression project is outside the Git workspace.");
        }

        if (!File.Exists(projectPath))
        {
            return Fail(
                $"Regression project was not found: " +
                $"{_options.RegressionProject}");
        }

        if (!string.Equals(
                Path.GetExtension(projectPath),
                ".csproj",
                StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                "Regression project must be a .csproj file.");
        }

        return new RegressionValidationResult
        {
            Success = true,
            Reproduced = false,
            StatusCode = null,
            Output =
                $"Regression project validated: " +
                $"{_options.RegressionProject}",
            ErrorOutput = string.Empty
        };
    }

    private static async Task<bool> WaitUntilReadyAsync(
        HttpClient client,
        string baseUrl,
        Process process,
        CancellationToken cancellationToken)
    {
        using var timeoutCts =
            new CancellationTokenSource(StartupTimeout);

        using var linkedCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeoutCts.Token);

        while (!linkedCts.IsCancellationRequested)
        {
            if (process.HasExited)
            {
                return false;
            }

            try
            {
                using var response =
                    await client.GetAsync(
                        baseUrl,
                        linkedCts.Token);

                return true;
            }
            catch (HttpRequestException)
            {
                // Application is still starting.
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            await Task.Delay(
                100,
                linkedCts.Token);
        }

        return false;
    }

    private static string BuildRequestUri(
        string baseUrl,
        string path,
        string? queryString)
    {
        var normalizedPath =
            path.StartsWith('/')
                ? path
                : $"/{path}";

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return baseUrl + normalizedPath;
        }

        var normalizedQuery =
            queryString.StartsWith('?')
                ? queryString
                : $"?{queryString}";

        return baseUrl +
               normalizedPath +
               normalizedQuery;
    }

    private static int GetFreeTcpPort()
    {
        var listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        try
        {
            return ((IPEndPoint)
                listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static void TryKill(
        Process process)
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

    private static RegressionValidationResult Fail(
        string error)
    {
        return new RegressionValidationResult
        {
            Success = false,
            Reproduced = false,
            StatusCode = null,
            Output = string.Empty,
            ErrorOutput = error
        };
    }

    private static bool IsInsideRoot(
        string root,
        string candidate)
    {
        var relative =
            Path.GetRelativePath(
                root,
                candidate);

        return relative != ".." &&
               !relative.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal) &&
               !Path.IsPathRooted(relative);
    }
}