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

            return new SuggestedFix
            {
                Summary =
                    $"Add null handling for '{variableName}' before it is dereferenced.",

                Confidence = "high",

                ReplacementCode =
                    $"if ({variableName} is null)\n" +
                    "{\n" +
                    "    throw new InvalidOperationException(\n" +
                    $"        \"{variableName} must not be null.\");\n" +
                    "}\n\n" +
                    $"return {variableName}.ToUpper();",

                Notes =
                    "This is a rule-based suggestion. The exact production fix may depend on business requirements."
            };
        }

        return new SuggestedFix
        {
            Summary = "No reliable automatic fix could be generated.",
            Confidence = "low",
            Notes = "Manual review or deeper analysis is required."
        };
    }
}