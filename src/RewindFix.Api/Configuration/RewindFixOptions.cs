namespace RewindFix.Api.Configuration;

public sealed class RewindFixOptions
{
    public AutomationLevel AutomationLevel { get; set; }
        = AutomationLevel.Analyze;
}