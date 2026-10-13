namespace RewindFix.Api.Configuration;

public sealed class GitHubOptions
{
    public string Owner { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public string BaseBranch { get; set; } = "main";

    public string Token { get; set; } = string.Empty;
}