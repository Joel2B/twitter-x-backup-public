using Backup.Application.Media.Filter;

namespace Backup.Tests;

public class MediaDownloadFilterPolicyServiceTests
{
    private readonly MediaDownloadFilterPolicyService _sut = new();

    [Fact]
    public void Parse_ReturnsEmpty_WhenFiltersMissing()
    {
        IReadOnlyList<MediaExclusionRule> filters = _sut.Parse(null);

        Assert.Empty(filters);
    }

    [Fact]
    public void Parse_SkipsInvalidEntries_AndNormalizesCase()
    {
        IReadOnlyList<MediaExclusionRule> filters = _sut.Parse(
            ["JPG:Orig:4096x4096", "invalid", "png:thumb:*"]
        );

        Assert.Equal(2, filters.Count);
        Assert.Contains(
            filters,
            filter =>
                filter.Extension == "jpg"
                && filter.FormatType == "orig"
                && filter.ResolutionName == "4096x4096"
        );
        Assert.Contains(
            filters,
            filter =>
                filter.Extension == "png"
                && filter.FormatType == "thumb"
                && filter.ResolutionName == "*"
        );
    }

    [Fact]
    public void IsExcluded_ReturnsTrue_WhenWildcardMatches()
    {
        IReadOnlyList<MediaExclusionRule> filters = _sut.Parse(["jpg:*:*"]);

        bool excluded = _sut.IsExcluded(filters, "JPG", "orig", "4096x4096");

        Assert.True(excluded);
    }

    [Fact]
    public void IsExcluded_ReturnsFalse_WhenNoRuleMatches()
    {
        IReadOnlyList<MediaExclusionRule> filters = _sut.Parse(["png:thumb:*"]);

        bool excluded = _sut.IsExcluded(filters, "jpg", "orig", "4096x4096");

        Assert.False(excluded);
    }
}
