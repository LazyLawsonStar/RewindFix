using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RewindFix.SampleApi.Tests;

public sealed class CrashEndpointTests
{
    [Fact]
    public async Task CrashEndpoint_ShouldNotHangWhenRewindFixServerIsUnavailable()
    {
        await using var factory =
            new WebApplicationFactory<Program>();

        var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();

        await client.GetAsync("/api/crash");

        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Crash handling took too long: " +
            $"{stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }
}