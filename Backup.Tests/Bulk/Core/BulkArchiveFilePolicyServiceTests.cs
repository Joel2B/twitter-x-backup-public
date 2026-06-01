using Backup.Application.Bulk;

namespace Backup.Tests;

public class BulkArchiveFilePolicyServiceTests
{
    private readonly BulkArchiveFilePolicyService _sut = new();

    [Fact]
    public void BuildArchivePath_UsesDirectoryOfCurrentFile()
    {
        string currentPath = Path.Combine("base", "bulk", "current.json");
        DateTime now = new(2026, 5, 30, 4, 5, 6);

        string archivePath = _sut.BuildArchivePath(currentPath, now);

        Assert.Equal(Path.Combine("base", "bulk", "2026.05.30-04.05.06.json"), archivePath);
    }

    [Fact]
    public void BuildArchivePath_UsesTimestampFormat()
    {
        string currentPath = "current.json";
        DateTime now = new(2026, 12, 1, 23, 59, 58);

        string archivePath = _sut.BuildArchivePath(currentPath, now);

        Assert.EndsWith("2026.12.01-23.59.58.json", archivePath, StringComparison.Ordinal);
    }
}
