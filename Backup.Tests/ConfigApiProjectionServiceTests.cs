using Backup.Application.Config;
using Backup.Application.Config.Models;

namespace Backup.Tests;

public class ConfigApiProjectionServiceTests
{
    private readonly IConfigApiProjectionService _sut = new ConfigApiProjectionService();

    [Fact]
    public void ToEntries_And_ToProjections_RoundTrip()
    {
        IReadOnlyDictionary<string, ConfigApiProjection> source = new Dictionary<
            string,
            ConfigApiProjection
        >
        {
            ["Api.SearchTimeline"] = new ConfigApiProjection
            {
                Key = "Api.SearchTimeline",
                Id = "id-1",
                Url = "https://example.com",
                Variables = new Dictionary<string, object?> { ["count"] = 20 },
                Features = new Dictionary<string, bool> { ["f"] = true },
                FieldToggles = new Dictionary<string, bool> { ["t"] = false },
                Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer x" },
            },
        };

        IReadOnlyList<ConfigApiEntry> entries = _sut.ToEntries(source);
        IReadOnlyDictionary<string, ConfigApiProjection> back = _sut.ToProjections(entries);

        Assert.Single(entries);
        Assert.True(back.ContainsKey("Api.SearchTimeline"));
        Assert.Equal("id-1", back["Api.SearchTimeline"].Id);
        Assert.Equal(20, back["Api.SearchTimeline"].Variables["count"]);
    }
}
