namespace RewindFix.Api.Analysis;

public sealed class PatchApplier
{
    public async Task<PatchApplyResult> ApplyPreviewAsync(
        CodePatch patch)
    {
        if (!File.Exists(patch.FilePath))
        {
            return new PatchApplyResult
            {
                Success = false,
                OutputFilePath = patch.FilePath,
                Error = "Source file does not exist."
            };
        }

        var lines = await File.ReadAllLinesAsync(
            patch.FilePath);

        var result = ApplyToLines(
            lines,
            patch);

        if (!result.Success)
        {
            return new PatchApplyResult
            {
                Success = false,
                OutputFilePath = patch.FilePath,
                Error = result.Error
            };
        }

        var directory =
            Path.GetDirectoryName(patch.FilePath)
            ?? throw new InvalidOperationException(
                "Unable to determine source directory.");

        var fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(
                patch.FilePath);

        var extension =
            Path.GetExtension(patch.FilePath);

        var outputFilePath = Path.Combine(
            directory,
            $"{fileNameWithoutExtension}.rewindfix{extension}");

        await File.WriteAllLinesAsync(
            outputFilePath,
            result.Lines!);

        return new PatchApplyResult
        {
            Success = true,
            OutputFilePath = outputFilePath
        };
    }

    public async Task ApplyAsync(
        string filePath,
        CodePatch patch,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Patch target file does not exist.",
                filePath);
        }

        var lines = await File.ReadAllLinesAsync(
            filePath,
            cancellationToken);

        var result = ApplyToLines(
            lines,
            patch);

        if (!result.Success)
        {
            throw new InvalidOperationException(
                result.Error);
        }

        await File.WriteAllLinesAsync(
            filePath,
            result.Lines!,
            cancellationToken);
    }

    private static PatchLinesResult ApplyToLines(
        string[] lines,
        CodePatch patch)
    {
        var targetIndex =
            patch.TargetLine - 1;

        if (targetIndex < 0 ||
            targetIndex >= lines.Length)
        {
            return PatchLinesResult.Fail(
                "Target line is outside the file range.");
        }

        if (!string.Equals(
                lines[targetIndex].Trim(),
                patch.OriginalCode.Trim(),
                StringComparison.Ordinal))
        {
            return PatchLinesResult.Fail(
                "Target line no longer matches the original code.");
        }

        var replacementLines =
            patch.ReplacementCode
                .Replace("\r\n", "\n")
                .Split('\n');

        var indentation =
            GetIndentation(lines[targetIndex]);

        var newLines = new List<string>();

        newLines.AddRange(
            lines[..targetIndex]);

        foreach (var replacementLine in replacementLines)
        {
            if (string.IsNullOrWhiteSpace(
                    replacementLine))
            {
                newLines.Add(string.Empty);
                continue;
            }

            newLines.Add(
                indentation +
                replacementLine.TrimStart());
        }

        newLines.AddRange(
            lines[(targetIndex + 1)..]);

        return new PatchLinesResult
        {
            Success = true,
            Lines = newLines.ToArray()
        };
    }

    private static string GetIndentation(
        string line)
    {
        var length =
            line.Length -
            line.TrimStart().Length;

        return line[..length];
    }

    private sealed record PatchLinesResult
    {
        public required bool Success { get; init; }

        public string[]? Lines { get; init; }

        public string? Error { get; init; }

        public static PatchLinesResult Fail(
            string error)
        {
            return new PatchLinesResult
            {
                Success = false,
                Error = error
            };
        }
    }
}