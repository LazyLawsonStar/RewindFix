using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RewindFix.SampleApi.Tests;

public sealed class CrashEndpointTests
{
    [Fact]
    public async Task CrashEndpoint_ShouldReturnServerError()
    {
        await using var factory =
            new WebApplicationFactory<Program>();

        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crash");

        Assert.True(
            (int)response.StatusCode >= 500,
            $"Expected crash endpoint to return a server error, but received {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task CrashEndpoint_WhenRewindFixServerIsUnavailable_ShouldNotHangIndefinitely()
    {
        await using var factory =
            new WebApplicationFactory<Program>();

        var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();

        var response = await client.GetAsync("/api/crash");

        stopwatch.Stop();

        Assert.True(
            (int)response.StatusCode >= 500,
            $"Expected crash endpoint to return a server error, but received {(int)response.StatusCode}.");

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(1),
            $"Crash handling took too long: {stopwatch.Elapsed.TotalMilliseconds:F0} ms.");
    }
}