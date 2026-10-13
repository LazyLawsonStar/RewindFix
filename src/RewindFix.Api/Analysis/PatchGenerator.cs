namespace RewindFix.Api.Analysis;

public sealed class PatchGenerator
{
    public CodePatch? Generate(
        SourceContext? sourceContext,
        SuggestedFix suggestedFix)
    {
        if (sourceContext is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(
                suggestedFix.ReplacementCode))
        {
            return null;
        }

        var targetLine = sourceContext.Lines
            .FirstOrDefault(x => x.IsTarget);

        if (targetLine is null)
        {
            return null;
        }

        return new CodePatch
        {
            FilePath = sourceContext.FilePath,
            TargetLine = targetLine.LineNumber,
            OriginalCode = targetLine.Code,
            ReplacementCode = suggestedFix.ReplacementCode,
            Confidence = suggestedFix.Confidence
        };
    }
}