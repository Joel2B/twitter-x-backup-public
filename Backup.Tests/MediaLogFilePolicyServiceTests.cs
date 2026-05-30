using Backup.Application.Media;

namespace Backup.Tests;

public class MediaLogFilePolicyServiceTests
{
    private readonly MediaLogFilePolicyService _sut = new();

    [Fact]
    public void CreateFileName_UsesExpectedFormat()
    {
        string fileName = _sut.CreateFileName(new DateTime(2026, 5, 30, 4, 5, 6));

        Assert.Equal("2026.05.30-04.05.06.json", fileName);
    }

    [Fact]
    public void SelectLatestFilePath_ReturnsLatestValidTimestamp()
    {
        List<string> paths =
        [
            Path.Combine("x", "2026.05.30-04.05.06.json"),
            Path.Combine("x", "invalid.json"),
            Path.Combine("x", "2026.05.30-04.05.07.json"),
        ];

        string? latest = _sut.SelectLatestFilePath(paths);

        Assert.Equal(Path.Combine("x", "2026.05.30-04.05.07.json"), latest);
    }

    [Fact]
    public void SelectLatestFilePath_ReturnsNull_WhenNoValidPaths()
    {
        string? latest = _sut.SelectLatestFilePath([Path.Combine("x", "invalid.json")]);

        Assert.Null(latest);
    }
}
