using Backup.Application.Media.Backup;

namespace Backup.Tests;

public sealed class MediaBackupChunkFileNamePolicyServiceTests
{
    [Fact]
    public void BuildDataFileName_ComposesChunkIdAndExtension()
    {
        MediaBackupChunkFileNamePolicyService sut = new();

        string fileName = sut.BuildDataFileName(7, "json");

        Assert.Equal("7.json", fileName);
    }

    [Fact]
    public void BuildZipFileName_ComposesChunkIdAndExtension()
    {
        MediaBackupChunkFileNamePolicyService sut = new();

        string fileName = sut.BuildZipFileName(8, "zip");

        Assert.Equal("8.zip", fileName);
    }
}
