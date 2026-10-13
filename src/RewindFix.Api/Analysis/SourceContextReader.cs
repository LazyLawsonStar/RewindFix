namespace RewindFix.Api.Analysis;

public sealed class SourceContextReader
{
    public async Task<SourceContext?> ReadAsync(
        string? filePath,
        int? lineNumber,
        int radius = 5)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        if (lineNumber is null || lineNumber <= 0)
        {
            return null;
        }

        if (!File.Exists(filePath))
        {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(filePath);

        if (lineNumber > lines.Length)
        {
            return null;
        }

        var targetIndex = lineNumber.Value - 1;

        var startIndex = Math.Max(0, targetIndex - radius);
        var endIndex = Math.Min(
            lines.Length - 1,
            targetIndex + radius);

        var contextLines = new List<SourceContextLine>();

        for (var i = startIndex; i <= endIndex; i++)
        {
            contextLines.Add(new SourceContextLine
            {
                LineNumber = i + 1,
                Code = lines[i],
                IsTarget = i == targetIndex
            });
        }

        return new SourceContext
        {
            FilePath = filePath,
            TargetLine = lineNumber.Value,
            Lines = contextLines
        };
    }
}

public sealed record SourceContext
{
    public required string FilePath { get; init; }

    public required int TargetLine { get; init; }

    public required IReadOnlyList<SourceContextLine> Lines { get; init; }
}

public sealed record SourceContextLine
{
    public required int LineNumber { get; init; }

    public required string Code { get; init; }

    public required bool IsTarget { get; init; }
}