using Microsoft.Extensions.Options;
using RewindFix.Api.Configuration;

namespace RewindFix.Api.Analysis;

public sealed class SourceContextReader
{
    private readonly string? _repositoryRoot;

    public SourceContextReader(
        IOptions<RewindFixOptions> options)
    {
        var configuredRoot = options.Value.RepositoryRoot;

        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            _repositoryRoot = Path.GetFullPath(configuredRoot);
        }
    }

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

        if (string.IsNullOrWhiteSpace(_repositoryRoot))
        {
            return null;
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return null;
        }

        if (!IsInsideRepository(fullPath))
        {
            return null;
        }

        if (!File.Exists(fullPath))
        {
            return null;
        }

        var lines = await File.ReadAllLinesAsync(fullPath);

        if (lineNumber > lines.Length)
        {
            return null;
        }

        var targetIndex = lineNumber.Value - 1;

        var startIndex = Math.Max(
            0,
            targetIndex - radius);

        var endIndex = Math.Min(
            lines.Length - 1,
            targetIndex + radius);

        var contextLines =
            new List<SourceContextLine>();

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
            FilePath = fullPath,
            TargetLine = lineNumber.Value,
            Lines = contextLines
        };
    }

    private bool IsInsideRepository(
        string fullPath)
    {
        if (string.IsNullOrWhiteSpace(_repositoryRoot))
        {
            return false;
        }

        var relativePath = Path.GetRelativePath(
            _repositoryRoot,
            fullPath);

        return relativePath != ".." &&
               !relativePath.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal) &&
               !Path.IsPathRooted(relativePath);
    }
}

public sealed record SourceContext
{
    public required string FilePath { get; init; }

    public required int TargetLine { get; init; }

    public required IReadOnlyList<SourceContextLine>
        Lines { get; init; }
}

public sealed record SourceContextLine
{
    public required int LineNumber { get; init; }

    public required string Code { get; init; }

    public required bool IsTarget { get; init; }
}