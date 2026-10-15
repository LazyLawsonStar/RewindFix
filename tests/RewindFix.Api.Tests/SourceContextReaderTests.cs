using Microsoft.Extensions.Options;
using RewindFix.Api.Analysis;
using RewindFix.Api.Configuration;

namespace RewindFix.Api.Tests;

public sealed class SourceContextReaderTests : IDisposable
{
    private readonly string _repositoryRoot;
    private readonly string _outsideRoot;

    public SourceContextReaderTests()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "RewindFixTests",
            Guid.NewGuid().ToString("N"));

        _repositoryRoot = Path.Combine(
            testRoot,
            "repository");

        _outsideRoot = Path.Combine(
            testRoot,
            "outside");

        Directory.CreateDirectory(_repositoryRoot);
        Directory.CreateDirectory(_outsideRoot);
    }

    [Fact]
    public async Task ReadAsync_FileInsideRepository_ReturnsSourceContext()
    {
        var filePath = Path.Combine(
            _repositoryRoot,
            "Example.cs");

        await File.WriteAllLinesAsync(
            filePath,
            [
                "line 1",
                "line 2",
                "line 3"
            ]);

        var reader = CreateReader();

        var result = await reader.ReadAsync(
            filePath,
            lineNumber: 2);

        Assert.NotNull(result);
        Assert.Equal(
            Path.GetFullPath(filePath),
            result.FilePath);

        Assert.Equal(2, result.TargetLine);

        Assert.Contains(
            result.Lines,
            x => x.LineNumber == 2 &&
                 x.Code == "line 2" &&
                 x.IsTarget);
    }

    [Fact]
    public async Task ReadAsync_FileOutsideRepository_ReturnsNull()
    {
        var filePath = Path.Combine(
            _outsideRoot,
            "secret.txt");

        await File.WriteAllTextAsync(
            filePath,
            "TOP SECRET");

        var reader = CreateReader();

        var result = await reader.ReadAsync(
            filePath,
            lineNumber: 1);

        Assert.Null(result);
    }

    private SourceContextReader CreateReader()
    {
        var options = Options.Create(
            new RewindFixOptions
            {
                RepositoryRoot = _repositoryRoot
            });

        return new SourceContextReader(options);
    }

    public void Dispose()
    {
        var testRoot =
            Directory.GetParent(_repositoryRoot)!
                .FullName;

        if (Directory.Exists(testRoot))
        {
            Directory.Delete(
                testRoot,
                recursive: true);
        }
    }
}