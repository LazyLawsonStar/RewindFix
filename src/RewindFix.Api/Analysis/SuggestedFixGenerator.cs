namespace RewindFix.Api.Analysis;

public sealed class SuggestedFixGenerator
{
    public SuggestedFix Generate(
        RootCauseAnalysis rootCause,
        SourceContext? sourceContext)
    {
        if (sourceContext is null)
        {
            return new SuggestedFix
            {
                Summary = "Unable to generate a fix without source context.",
                Confidence = "low",
                Notes = "Source code around the crash location is unavailable."
            };
        }

        if (rootCause.Confidence == "high" &&
            !string.IsNullOrWhiteSpace(rootCause.VariableName))
        {
            var variableName = rootCause.VariableName;

            var targetLine = sourceContext.Lines
                .FirstOrDefault(x => x.IsTarget);

            if (targetLine is not null &&
                targetLine.Code.Contains(
                    $"{variableName}!.ToUpper()",
                    StringComparison.Ordinal))
            {
                return new SuggestedFix
                {
                    Summary =
                        $"Make the ToUpper call null-safe for '{variableName}'.",

                    Confidence = "high",

                    ReplacementCode =
                        $"return {variableName}?.ToUpper() ?? string.Empty;",

                    Notes =
                        "This rule prevents the NullReferenceException and preserves a string return value. Business-specific behavior may require a different fallback."
                };
            }
        }

        return new SuggestedFix
        {
            Summary = "No reliable automatic fix could be generated.",
            Confidence = "low",
            Notes =
                "RewindFix does not have a safe rule for this code pattern yet."
        };
    }
}