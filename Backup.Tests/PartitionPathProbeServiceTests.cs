using Backup.Application.Partition;

namespace Backup.Tests;

public sealed class PartitionPathProbeServiceTests
{
    [Fact]
    public void Probe_WhenPathIsWritable_ReturnsNullAndDeletesProbeFile()
    {
        PartitionPathProbeService sut = new();
        string tempRoot = Path.Combine(
            Path.GetTempPath(),
            "backup-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(tempRoot);
        string probePath = Path.Combine(tempRoot, "probe.tmp");

        try
        {
            string? error = sut.Probe(probePath);

            Assert.Null(error);
            Assert.False(File.Exists(probePath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void Probe_WhenPathIsInvalid_ReturnsErrorMessage()
    {
        PartitionPathProbeService sut = new();
        string missingRoot = Path.Combine(
            Path.GetTempPath(),
            "backup-tests-missing",
            Guid.NewGuid().ToString("N")
        );
        string probePath = Path.Combine(missingRoot, "probe.tmp");

        string? error = sut.Probe(probePath);

        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
