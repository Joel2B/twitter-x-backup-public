using Backup.Infrastructure.Services.Utils;

namespace Backup.Tests;

public sealed class ZipWriterTests
{
    [Fact]
    public async Task RemoveEntry_Uses_Exact_FullName_Match()
    {
        await using MemoryStream stream = new();
        using ZipWriter zip = new(stream, System.IO.Compression.ZipArchiveMode.Update);

        await zip.AddEntry("folder/abc.json", CreateContent());
        await zip.AddEntry("folder/abc2.json", CreateContent());
        await zip.AddEntry("folder/xabcx.json", CreateContent());

        Assert.True(zip.RemoveEntry("folder/abc.json"));
        Assert.False(zip.RemoveEntry("folder/abc.json"));
        Assert.True(zip.RemoveEntry("folder/abc2.json"));
        Assert.True(zip.RemoveEntry("folder/xabcx.json"));
    }

    [Fact]
    public async Task RemoveEntry_Duplicate_Mode_Does_Not_Match_Partial_Names()
    {
        await using MemoryStream stream = new();
        using ZipWriter zip = new(stream, System.IO.Compression.ZipArchiveMode.Update);

        await zip.AddEntry("a/abc.json", CreateContent());
        await zip.AddEntry("a/abc2.json", CreateContent());

        Assert.False(zip.RemoveEntry("abc", duplicate: true, skip: 1));
        Assert.True(zip.RemoveEntry("a/abc2.json"));
        Assert.True(zip.RemoveEntry("a/abc.json"));
    }

    private static Stream CreateContent() =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes("x"));
}
