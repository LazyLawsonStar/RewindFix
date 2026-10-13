using Microsoft.AspNetCore.Mvc.Testing;

namespace RewindFix.SampleApi.Tests;

public sealed class CrashEndpointTests
{
    [Fact]
    public async Task CrashEndpoint_ShouldNotReturnServerError()
    {
        await using var factory =
            new WebApplicationFactory<Program>();

        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/crash");

        Assert.False(
            (int)response.StatusCode >= 500,
            $"Expected endpoint not to return a server error, but received {(int)response.StatusCode}.");
    }
}