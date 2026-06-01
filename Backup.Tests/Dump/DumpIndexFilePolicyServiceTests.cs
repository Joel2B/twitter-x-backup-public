using Backup.Application.Dump;

namespace Backup.Tests;

public class DumpIndexFilePolicyServiceTests
{
    private readonly DumpIndexFilePolicyService _sut = new();

    [Fact]
    public void SelectIndexFiles_ReturnsOnlyNumericJsonOutsideApiFolder()
    {
        List<string> paths =
        [
            Path.Combine("root", "user", "0", "0.json"),
            Path.Combine("root", "user", "0", "meta.json"),
            Path.Combine("root", "user", "0", "api", "0.json"),
            Path.Combine("root", "user", "1", "10.json"),
        ];

        IReadOnlyList<string> result = _sut.SelectIndexFiles(paths, ["api"]);

        Assert.Equal(2, result.Count);
        Assert.Contains(Path.Combine("root", "user", "0", "0.json"), result);
        Assert.Contains(Path.Combine("root", "user", "1", "10.json"), result);
    }

    [Fact]
    public void SelectIndexFiles_UsesApiPathParts()
    {
        List<string> paths =
        [
            Path.Combine("root", "user", "0", "api", "search", "1.json"),
            Path.Combine("root", "user", "0", "1.json"),
        ];

        IReadOnlyList<string> result = _sut.SelectIndexFiles(paths, ["api", "search"]);

        Assert.Single(result);
        Assert.Equal(Path.Combine("root", "user", "0", "1.json"), result[0]);
    }
}
