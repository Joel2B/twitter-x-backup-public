using Backup.Application.IO;

namespace Backup.Tests;

public class PathFormattingPolicyTests
{
    [Fact]
    public void ParseTimestampFromPath_ReturnsDate_ForFileName()
    {
        DateTime? result = PathFormattingPolicy.ParseTimestampFromPath(
            @"C:\temp\2026.05.30-12.34.56.json"
        );

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 5, 30, 12, 34, 56), result.Value);
    }

    [Fact]
    public void ParseTimestampFromPath_ReturnsDate_ForDirectoryName()
    {
        DateTime? result = PathFormattingPolicy.ParseTimestampFromPath(
            @"C:\temp\2026.05.30-12.34.56",
            isDirectory: true
        );

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 5, 30, 12, 34, 56), result.Value);
    }

    [Fact]
    public void ParseTimestampFromPath_ReturnsNull_ForInvalidName()
    {
        DateTime? result = PathFormattingPolicy.ParseTimestampFromPath(@"C:\temp\posts.json");

        Assert.Null(result);
    }

    [Fact]
    public void GetFormattedPath_AppendsFormattedSuffixBeforeExtension()
    {
        string result = PathFormattingPolicy.GetFormattedPath(@"C:\temp\posts.json");

        Assert.Equal(@"C:\temp\posts.formatted.json", result);
    }

    [Fact]
    public void NormalizePathForCurrentOs_Windows_ReturnsSamePath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string input = @"C:\temp\a\b.json";
        string result = PathFormattingPolicy.NormalizePathForCurrentOs(input, save: false);

        Assert.Equal(input, result);
    }

    [Fact]
    public void NormalizePathForCurrentOs_Linux_ConvertsToForwardSlashForRead()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string result = PathFormattingPolicy.NormalizePathForCurrentOs(@"C:\temp\a\b.json", false);

        Assert.Equal(@"C:/temp/a/b.json", result);
    }

    [Fact]
    public void NormalizePathForCurrentOs_Linux_ConvertsToBackSlashForSave()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string result = PathFormattingPolicy.NormalizePathForCurrentOs(@"C:/temp/a/b.json", true);

        Assert.Equal(@"C:\temp\a\b.json", result);
    }
}
