namespace RewindFix.Api.Analysis;

public sealed record FixPipelineResult
{
    public required bool Success { get; init; }

    public string? Error { get; init; }

    public PatchValidationResult? BuildValidation
    {
        get;
        init;
    }

    public TestValidationResult? TestValidation
    {
        get;
        init;
    }

    public GitPatchResult? Git
    {
        get;
        init;
    }

    public static FixPipelineResult Fail(
        string error)
    {
        return new FixPipelineResult
        {
            Success = false,
            Error = error
        };
    }
}