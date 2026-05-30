using Backup.Application.IO;

namespace Backup.Tests;

public class PathAliasResolutionPolicyTests
{
    [Fact]
    public void ResolveAliases_ResolvesAliasAndKeepsRawPath()
    {
        IReadOnlyList<string> result = PathAliasResolutionPolicy.ResolveAliases(
            ["@root", "posts", "@archive"],
            new Dictionary<string, string> { ["root"] = "#Abs", ["archive"] = "backup" }
        );

        Assert.Equal(["#Abs", "posts", "backup"], result);
    }

    [Fact]
    public void ResolveAliases_Throws_WhenAliasIsMissing()
    {
        Exception ex = Assert.Throws<Exception>(() =>
            PathAliasResolutionPolicy.ResolveAliases(["@missing"], new Dictionary<string, string>())
        );

        Assert.Equal("alias '@missing' is not set", ex.Message);
    }
}
