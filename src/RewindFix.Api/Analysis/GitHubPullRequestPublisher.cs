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

    public GitHubPullRequestPublisher(
        HttpClient httpClient,
        IOptions<GitHubOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PullRequestResult> CreateAsync(
        string branchName,
        CodePatch patch)
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

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{_options.Owner}/{_options.Repository}/pulls");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                _options.Token);

        request.Headers.UserAgent.ParseAdd("RewindFix");

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
                {patch.FilePath}

                Line:
                {patch.TargetLine}

                Confidence:
                {patch.Confidence}

                Build validation passed.
                Test validation passed.
                """
        });

        try
        {
            var response =
                await _httpClient.SendAsync(request);

            var content =
                await response.Content.ReadAsStringAsync();

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
        catch (Exception exception)
        {
            return new PullRequestResult
            {
                Success = false,
                Error = exception.Message
            };
        }
    }
}