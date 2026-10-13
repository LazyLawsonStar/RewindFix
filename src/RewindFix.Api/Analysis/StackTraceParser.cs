using System.Text.RegularExpressions;

namespace RewindFix.Api.Analysis;

public sealed class StackTraceParser
{
    private static readonly Regex SourceRegex = new(
        @"^\s*at\s+(?<method>.+?)\s+in\s+(?<file>.+):line\s+(?<line>\d+)\s*$",
        RegexOptions.Compiled);

    public SourceLocation? Parse(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            return null;
        }

        var lines = stackTrace.Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var stackLine in lines)
        {
            var match = SourceRegex.Match(stackLine);

            if (!match.Success)
            {
                continue;
            }

            var filePath = match.Groups["file"].Value;

            if (!int.TryParse(
                    match.Groups["line"].Value,
                    out var lineNumber))
            {
                continue;
            }

            return new SourceLocation
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Line = lineNumber,
                Method = match.Groups["method"].Value
            };
        }

        return null;
    }
}