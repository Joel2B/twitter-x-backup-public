using Backup.Application.Media.Filter;

namespace Backup.Tests;

public class MediaErrorExclusionServiceTests
{
    [Fact]
    public void GetExcludedIds_ReturnsIds_WhenMessagesMatchPolicy()
    {
        MediaErrorExclusionService sut = new(new MediaErrorFilterPolicyService());
        MediaErrorMessage[] messages =
        [
            new() { Id = "1", Message = "NotFound" },
            new() { Id = "2", Message = "Forbidden" },
            new() { Id = "3", Message = "Timeout" },
        ];

        IReadOnlySet<string> excluded = sut.GetExcludedIds(messages);

        Assert.Equal(2, excluded.Count);
        Assert.Contains("1", excluded);
        Assert.Contains("2", excluded);
        Assert.DoesNotContain("3", excluded);
    }
}
