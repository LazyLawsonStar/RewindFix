using System.Text.RegularExpressions;

namespace RewindFix.Api.Analysis;

public sealed class RootCauseAnalyzer
{
    private static readonly Regex NullAssignmentRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*null\s*;",
        RegexOptions.Compiled);

    public RootCauseAnalysis Analyze(
        string exceptionType,
        SourceContext? sourceContext)
    {
        if (sourceContext is null)
        {
            return new RootCauseAnalysis
            {
                Summary = "Source code context is not available.",
                Confidence = "low",
                SuggestedAction = "Provide source code context for deeper analysis."
            };
        }

        if (exceptionType == "System.NullReferenceException")
        {
            return AnalyzeNullReference(sourceContext);
        }

        return new RootCauseAnalysis
        {
            Summary = $"No rule-based analyzer exists yet for {exceptionType}.",
            Confidence = "low",
            SuggestedAction = "Use deeper analysis or add a dedicated rule."
        };
    }

    private static RootCauseAnalysis AnalyzeNullReference(
        SourceContext sourceContext)
    {
        var targetLine = sourceContext.Lines
            .FirstOrDefault(x => x.IsTarget);

        if (targetLine is null)
        {
            return new RootCauseAnalysis
            {
                Summary = "A NullReferenceException occurred, but the target source line could not be found.",
                Confidence = "medium",
                SuggestedAction = "Inspect nullable values used near the crash location."
            };
        }

        foreach (var line in sourceContext.Lines)
        {
            var match = NullAssignmentRegex.Match(line.Code);

            if (!match.Success)
            {
                continue;
            }

            var variableName = match.Groups["name"].Value;

            if (targetLine.Code.Contains(variableName, StringComparison.Ordinal))
            {
                return new RootCauseAnalysis
                {
                    Summary =
                        $"The variable '{variableName}' is assigned null on line {line.LineNumber} " +
                        $"and is later dereferenced on line {targetLine.LineNumber}.",
                    Confidence = "high",
                    VariableName = variableName,
                    SuggestedAction =
                        $"Check '{variableName}' for null before dereferencing it, " +
                        "or ensure it cannot be null at this point."
                };
            }
        }

        return new RootCauseAnalysis
        {
            Summary =
                $"A NullReferenceException occurred on line {targetLine.LineNumber}. " +
                "One of the values used on this line is likely null.",
            Confidence = "medium",
            SuggestedAction =
                "Inspect values used on the target line and add appropriate null handling."
        };
    }
}