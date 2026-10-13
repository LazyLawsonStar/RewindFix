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

        var lines = await File.ReadAllLinesAsync(patch.FilePath);

        var targetIndex = patch.TargetLine - 1;

        if (targetIndex < 0 || targetIndex >= lines.Length)
        {
            return new PatchApplyResult
            {
                Success = false,
                OutputFilePath = patch.FilePath,
                Error = "Target line is outside the file range."
            };
        }

        if (!string.Equals(
                lines[targetIndex].Trim(),
                patch.OriginalCode.Trim(),
                StringComparison.Ordinal))
        {
            return new PatchApplyResult
            {
                Success = false,
                OutputFilePath = patch.FilePath,
                Error = "Target line no longer matches the original code."
            };
        }

        var replacementLines = patch.ReplacementCode
            .Replace("\r\n", "\n")
            .Split('\n');

        var newLines = new List<string>();

        newLines.AddRange(lines[..targetIndex]);

        var indentation = GetIndentation(lines[targetIndex]);

        foreach (var replacementLine in replacementLines)
        {
            if (string.IsNullOrWhiteSpace(replacementLine))
            {
                newLines.Add(string.Empty);
                continue;
            }

            newLines.Add(indentation + replacementLine);
        }

        newLines.AddRange(lines[(targetIndex + 1)..]);

        var directory = Path.GetDirectoryName(patch.FilePath)
            ?? throw new InvalidOperationException(
                "Unable to determine source directory.");

        var fileNameWithoutExtension =
            Path.GetFileNameWithoutExtension(patch.FilePath);

        var extension = Path.GetExtension(patch.FilePath);

        var outputFilePath = Path.Combine(
            directory,
            $"{fileNameWithoutExtension}.rewindfix{extension}");

        await File.WriteAllLinesAsync(
            outputFilePath,
            newLines);

        return new PatchApplyResult
        {
            Success = true,
            OutputFilePath = outputFilePath
        };
    }

    private static string GetIndentation(string line)
    {
        var length = line.Length - line.TrimStart().Length;

        return line[..length];
    }
}