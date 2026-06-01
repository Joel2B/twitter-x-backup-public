using Backup.Application.Diagnostics;

namespace Backup.Tests;

public class TextDiffPolicyTests
{
    [Fact]
    public void Diff_ReturnsLeftAndRightOnlyLines()
    {
        var result = TextDiffPolicy.Diff("a\nb\nc", "b\nc\nd");

        Assert.Equal(["a"], result.LeftOnlyLines);
        Assert.Equal(["d"], result.RightOnlyLines);
    }

    [Fact]
    public void Diff_IgnoresEmptyLines()
    {
        var result = TextDiffPolicy.Diff("a\n\nb", "a\r\nb\r\n");

        Assert.Empty(result.LeftOnlyLines);
        Assert.Empty(result.RightOnlyLines);
    }
}
