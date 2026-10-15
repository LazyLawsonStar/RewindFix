using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RewindFix.Api.Configuration;

namespace RewindFix.Api.Analysis;

public sealed class GitHubPullRequestPublisher
{
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly RewindFixOptions _rewindFixOptions;

    public GitHubPullRequestPublisher(
        HttpClient httpClient,
        IOptions<GitHubOptions> options,
        IOptions<RewindFixOptions> rewindFixOptions)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _rewindFixOptions = rewindFixOptions.Value;
    }

    public async Task<PullRequestResult> CreateAsync(
        string branchName,
        CodePatch patch,
        TestValidationResult testValidation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Token))
        {
            return new PullRequestResult
            {
                Success = false,
                Error = "GitHub token is not configured."
            };
        }

        if (string.IsNullOrWhiteSpace(_options.Owner) ||
            string.IsNullOrWhiteSpace(_options.Repository))
        {
            return new PullRequestResult
            {
                Success = false,
                Error = "GitHub repository configuration is missing."
            };
        }

        var relativeFilePath =
            GetRelativeFilePath(patch.FilePath);

        if (relativeFilePath is null)
        {
            return new PullRequestResult
            {
                Success = false,
                Error =
                    "Patch file is outside configured repository root."
            };
        }

        var testStatus = testValidation.TestsFound
            ? "Tests found and passed."
            : "No test projects were found.";

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{_options.Owner}/{_options.Repository}/pulls");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.Token);

        request.Headers.UserAgent.ParseAdd(
            "RewindFix");

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));

        request.Content = JsonContent.Create(new
        {
            title = "Apply RewindFix generated patch",
            head = branchName,
            @base = _options.BaseBranch,
            body =
                $"""
                Generated automatically by RewindFix.

                File:
                {relativeFilePath}

                Line:
                {patch.TargetLine}

                Confidence:
                {patch.Confidence}

                Validation:
                - Build passed.
                - {testStatus}
                """
        });

        try
        {
            var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            var content =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new PullRequestResult
                {
                    Success = false,
                    Error =
                        $"GitHub returned {(int)response.StatusCode}: {content}"
                };
            }

            using var document =
                JsonDocument.Parse(content);

            var root = document.RootElement;

            return new PullRequestResult
            {
                Success = true,
                Number =
                    root.GetProperty("number")
                        .GetInt32(),
                Url =
                    root.GetProperty("html_url")
                        .GetString()
            };
        }
        catch (OperationCanceledException)
        {
            return new PullRequestResult
            {
                Success = false,
                Error =
                    "GitHub Pull Request request was cancelled."
            };
        }
        catch (Exception exception)
        {
            return new PullRequestResult
            {
                Success = false,
                Error = exception.Message
            };
        }
    }

    private string? GetRelativeFilePath(
        string filePath)
    {
        if (string.IsNullOrWhiteSpace(
                _rewindFixOptions.RepositoryRoot))
        {
            return null;
        }

        var root = Path.GetFullPath(
            _rewindFixOptions.RepositoryRoot);

        var candidate =
            Path.GetFullPath(filePath);

        var relative =
            Path.GetRelativePath(
                root,
                candidate);

        if (relative == ".." ||
            relative.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            return null;
        }

        return relative.Replace(
            Path.DirectorySeparatorChar,
            '/');
    }
}