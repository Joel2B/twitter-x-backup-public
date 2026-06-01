using Backup.Application.IO;

namespace Backup.Tests;

public class FileHashPolicyTests
{
    [Fact]
    public async Task GetFileHash_ReturnsNull_WhenFileDoesNotExist()
    {
        string? result = await FileHashPolicy.GetFileHash(Path.GetTempFileName() + ".missing");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetFileHash_ReturnsExpectedSha256()
    {
        string filePath = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(filePath, "abc");
            string? hash = await FileHashPolicy.GetFileHash(filePath, "SHA256");

            Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", hash);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
