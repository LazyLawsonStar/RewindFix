using Microsoft.Extensions.Options;
using RewindFix.Api.Configuration;

namespace RewindFix.Api.Analysis;

public sealed class FixPipeline
{
    private readonly PatchApplier _patchApplier;
    private readonly PatchValidator _patchValidator;
    private readonly TestValidator _testValidator;
    private readonly GitPatchPublisher _gitPublisher;
    private readonly RewindFixOptions _options;

    public FixPipeline(
        PatchApplier patchApplier,
        PatchValidator patchValidator,
        TestValidator testValidator,
        GitPatchPublisher gitPublisher,
        IOptions<RewindFixOptions> options)
    {
        _patchApplier = patchApplier;
        _patchValidator = patchValidator;
        _testValidator = testValidator;
        _gitPublisher = gitPublisher;
        _options = options.Value;
    }

    public async Task<FixPipelineResult> ExecuteAsync(
        Guid crashId,
        CodePatch patch,
        bool publish,
        CancellationToken cancellationToken = default)
    {
        GitWorkspaceResult? workspace = null;

        try
        {
            var relativePath =
                GetTrustedRelativePath(patch.FilePath);

            if (relativePath is null)
            {
                return FixPipelineResult.Fail(
                    "Patch file is outside configured repository root.");
            }

            workspace =
                await _gitPublisher.CreateWorkspaceAsync(
                    crashId,
                    cancellationToken);

            if (!workspace.Success ||
                string.IsNullOrWhiteSpace(
                    workspace.WorkspacePath))
            {
                return FixPipelineResult.Fail(
                    workspace.Error ??
                    "Unable to create Git workspace.");
            }

            var workspaceFile =
                Path.GetFullPath(
                    Path.Combine(
                        workspace.WorkspacePath,
                        relativePath));

            var workspaceRoot =
                Path.GetFullPath(
                    workspace.WorkspacePath);

            if (!IsInsideRoot(
                    workspaceRoot,
                    workspaceFile))
            {
                return FixPipelineResult.Fail(
                    "Resolved patch path escaped workspace.");
            }

            await _patchApplier.ApplyAsync(
                workspaceFile,
                patch,
                cancellationToken);

            var build =
                await _patchValidator.ValidateAsync(
                    workspace.WorkspacePath,
                    cancellationToken);

            if (!build.Success)
            {
                return new FixPipelineResult
                {
                    Success = false,
                    Error =
                        "Generated patch did not pass build validation.",
                    BuildValidation = build
                };
            }

            var tests =
                await _testValidator.ValidateAsync(
                    workspace.WorkspacePath,
                    cancellationToken);

            if (!tests.Success)
            {
                return new FixPipelineResult
                {
                    Success = false,
                    Error =
                        "Generated patch did not pass tests.",
                    BuildValidation = build,
                    TestValidation = tests
                };
            }

            if (!publish)
            {
                return new FixPipelineResult
                {
                    Success = true,
                    BuildValidation = build,
                    TestValidation = tests
                };
            }

            var git =
                await _gitPublisher.PublishAsync(
                    workspace,
                    relativePath,
                    cancellationToken);

            if (!git.Success)
            {
                return new FixPipelineResult
                {
                    Success = false,
                    Error =
                        "Unable to publish validated patch.",
                    BuildValidation = build,
                    TestValidation = tests,
                    Git = git
                };
            }

            return new FixPipelineResult
            {
                Success = true,
                BuildValidation = build,
                TestValidation = tests,
                Git = git
            };
        }
        catch (OperationCanceledException)
        {
            return FixPipelineResult.Fail(
                "Fix pipeline was cancelled or timed out.");
        }
        catch (Exception exception)
        {
            return FixPipelineResult.Fail(
                exception.Message);
        }
        finally
        {
            if (workspace is not null)
            {
                await _gitPublisher.CleanupAsync(
                    workspace);
            }
        }
    }

    private string? GetTrustedRelativePath(
        string sourceFile)
    {
        if (string.IsNullOrWhiteSpace(
                _options.RepositoryRoot))
        {
            return null;
        }

        var root =
            Path.GetFullPath(
                _options.RepositoryRoot);

        var source =
            Path.GetFullPath(sourceFile);

        if (!IsInsideRoot(root, source))
        {
            return null;
        }

        return Path.GetRelativePath(
            root,
            source);
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