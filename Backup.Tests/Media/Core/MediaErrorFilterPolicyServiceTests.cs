using Backup.Application.Media.Filter;

namespace Backup.Tests;

public class MediaErrorFilterPolicyServiceTests
{
    [Theory]
    [InlineData("NotFound", true)]
    [InlineData("Forbidden", true)]
    [InlineData("Timeout", false)]
    [InlineData(null, false)]
    public void ShouldExclude_ReturnsExpected(string? message, bool expected)
    {
        MediaErrorFilterPolicyService sut = new();

        bool result = sut.ShouldExclude(message);

        Assert.Equal(expected, result);
    }
}
