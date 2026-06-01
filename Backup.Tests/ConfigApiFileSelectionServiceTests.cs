using Backup.Application.Config;

namespace Backup.Tests;

public class ConfigApiFileSelectionServiceTests
{
    [Fact]
    public void ValidateApiDirectoryExists_Throws_WhenDirectoryMissing()
    {
        ConfigApiFileSelectionService sut = new();

        Exception ex = Assert.Throws<Exception>(() => sut.ValidateApiDirectoryExists(false));

        Assert.Contains("directory does not exist", ex.Message);
    }

    [Fact]
    public void SelectRequiredFiles_ReturnsMap_WhenAllUserFilesExist()
    {
        ConfigApiFileSelectionService sut = new();

        IReadOnlyDictionary<string, string> result = sut.SelectRequiredFiles(
            ["u1", "u2"],
            ["u1.json", "u2.json", "extra.json"]
        );

        Assert.Equal("u1.json", result["u1"]);
        Assert.Equal("u2.json", result["u2"]);
    }

    [Fact]
    public void SelectRequiredFiles_Throws_WhenNoJsonFilesFound()
    {
        ConfigApiFileSelectionService sut = new();

        Exception ex = Assert.Throws<Exception>(() => sut.SelectRequiredFiles(["u1"], []));

        Assert.Contains("no json files found", ex.Message);
    }

    [Fact]
    public void SelectRequiredFiles_Throws_WhenUserFileMissing()
    {
        ConfigApiFileSelectionService sut = new();

        Exception ex = Assert.Throws<Exception>(
            () => sut.SelectRequiredFiles(["u1", "u2"], ["u1.json"])
        );

        Assert.Contains("file 'u2.json' not found for user 'u2'", ex.Message);
    }
}
