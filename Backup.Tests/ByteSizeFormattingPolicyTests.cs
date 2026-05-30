using Backup.Application.IO;

namespace Backup.Tests;

public class ByteSizeFormattingPolicyTests
{
    [Theory]
    [InlineData(-1, "0 B")]
    [InlineData(0, "0 B")]
    [InlineData(1, "1 B")]
    [InlineData(1024, "1 KiB")]
    [InlineData(1536, "1.5 KiB")]
    [InlineData(1048576, "1 MiB")]
    public void FormatBytes_ReturnsExpectedValue(long bytes, string expected)
    {
        string result = ByteSizeFormattingPolicy.FormatBytes(bytes);
        Assert.Equal(expected, result);
    }
}
