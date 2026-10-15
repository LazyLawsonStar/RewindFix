namespace RewindFix.AspNetCore;

public sealed class RewindFixOptions
{
    public string ServerUrl { get; set; }
        = "http://localhost:5000";

    public TimeSpan SendTimeout { get; set; }
        = TimeSpan.FromSeconds(3);

    public int QueueCapacity { get; set; }
        = 1000;
}