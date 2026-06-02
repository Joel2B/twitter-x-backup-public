using Backup.Application.IO;

namespace Backup.Tests;

public class DataStoreGuardServiceTests
{
    private readonly IDataStoreGuardService _sut = new DataStoreGuardService();

    [Fact]
    public void RequireConfiguredFileName_Throws_WhenEmpty()
    {
        Assert.Throws<InvalidOperationException>(() => _sut.RequireConfiguredFileName(null));
        Assert.Throws<InvalidOperationException>(() => _sut.RequireConfiguredFileName(""));
    }

    [Fact]
    public void RequireConfiguredFileName_ReturnsValue_WhenPresent()
    {
        string value = _sut.RequireConfiguredFileName("data.json");
        Assert.Equal("data.json", value);
    }

    [Fact]
    public void EnsureFileExists_Throws_WhenMissing()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".tmp");
        Assert.Throws<FileNotFoundException>(() => _sut.EnsureFileExists(path));
    }

    [Fact]
    public void RequireDeserialized_ReturnsValue_WhenNotNull()
    {
        string value = _sut.RequireDeserialized("ok", "err");
        Assert.Equal("ok", value);
    }
}
