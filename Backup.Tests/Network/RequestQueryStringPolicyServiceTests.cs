using Backup.Application.Network;

namespace Backup.Tests;

public class RequestQueryStringPolicyServiceTests
{
    private readonly RequestQueryStringPolicyService _sut = new();

    [Fact]
    public void Build_IncludesRequiredSections()
    {
        string url = _sut.Build(
            "https://x.com/i/api/graphql/id/Query",
            new Dictionary<string, object?> { ["count"] = 20, ["cursor"] = "abc" },
            new Dictionary<string, bool> { ["f1"] = true },
            new Dictionary<string, bool> { ["t1"] = false }
        );

        Assert.Contains("variables=", url, StringComparison.Ordinal);
        Assert.Contains("features=", url, StringComparison.Ordinal);
        Assert.Contains("fieldToggles=", url, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ExcludesNullVariables()
    {
        string url = _sut.Build(
            "https://x.com/i/api/graphql/id/Query",
            new Dictionary<string, object?> { ["count"] = 20, ["cursor"] = null },
            new Dictionary<string, bool>(),
            new Dictionary<string, bool>()
        );

        string decoded = Uri.UnescapeDataString(url);
        Assert.Contains("\"count\":20", decoded, StringComparison.Ordinal);
        Assert.DoesNotContain("\"cursor\":null", decoded, StringComparison.Ordinal);
    }
}
