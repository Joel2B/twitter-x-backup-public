using Backup.Application.Config;
using Backup.Application.Config.Models;

namespace Backup.Tests;

public class ConfigNormalizationServiceTests
{
    [Fact]
    public void NormalizeUserIds_TrimsValues()
    {
        ConfigNormalizationService sut = new();

        IReadOnlyList<string> users = sut.NormalizeUserIds(["  user-1  ", "user-2"]);

        Assert.Equal(["user-1", "user-2"], users);
    }

    [Fact]
    public void ValidateUsers_TrimsIds()
    {
        ConfigNormalizationService sut = new();
        List<ConfigUser> users = [new() { Id = "  user-1  " }];

        sut.ValidateUsers(users);

        Assert.Equal("user-1", users[0].Id);
    }

    [Fact]
    public void ValidateAndNormalizeApi_NormalizesCountAndCursor()
    {
        ConfigNormalizationService sut = new();

        ConfigApiEntry entry = new()
        {
            Key = "Api.SearchTimeline",
            Id = "id-1",
            Url = "https://example.com",
            Variables = new Dictionary<string, object?> { ["count"] = "5", ["flag"] = "true" },
            Features = null,
            FieldToggles = null,
            Headers = null,
        };

        sut.ValidateAndNormalizeApi([entry]);

        Assert.Equal(5, Assert.IsType<int>(entry.Variables["count"]));
        Assert.True(Assert.IsType<bool>(entry.Variables["flag"]));
        Assert.True(entry.Variables.ContainsKey("cursor"));
        Assert.Null(entry.Variables["cursor"]);
        Assert.NotNull(entry.Features);
        Assert.NotNull(entry.FieldToggles);
        Assert.NotNull(entry.Headers);
    }

    [Fact]
    public void ApplyFetchToApi_UpdatesCount()
    {
        ConfigNormalizationService sut = new();

        ConfigApiEntry api = new()
        {
            Key = "Api.SearchTimeline",
            Id = "id-1",
            Url = "https://example.com",
            Variables = new Dictionary<string, object?> { ["count"] = 20 },
            Features = [],
            FieldToggles = [],
            Headers = [],
        };

        ConfigFetchEntry fetch = new()
        {
            Key = "Api.SearchTimeline",
            CountRaw = 50,
            ApiRaw = 10,
            Count = 50,
            Api = 10,
        };

        sut.ApplyFetchToApi([api], [fetch]);

        Assert.Equal(10, Assert.IsType<int>(api.Variables["count"]));
        Assert.True(api.Variables.ContainsKey("cursor"));
    }

    [Fact]
    public void ValidateApiFileEntries_Throws_WhenIdMissing()
    {
        ConfigNormalizationService sut = new();
        Dictionary<string, ConfigApiFileEntry?> api = new()
        {
            ["Api.SearchTimeline"] = new ConfigApiFileEntry
            {
                Key = "Api.SearchTimeline",
                Id = null,
                HasRequest = true,
            },
        };

        Exception ex = Assert.Throws<Exception>(() =>
            sut.ValidateApiFileEntries("1122205668801257472.json", api)
        );

        Assert.Contains("missing required field 'Id'", ex.Message);
    }

    [Fact]
    public void ValidateApiFileEntries_Throws_WhenRequestMissing()
    {
        ConfigNormalizationService sut = new();
        Dictionary<string, ConfigApiFileEntry?> api = new()
        {
            ["Api.SearchTimeline"] = new ConfigApiFileEntry
            {
                Key = "Api.SearchTimeline",
                Id = "api-id",
                HasRequest = false,
            },
        };

        Exception ex = Assert.Throws<Exception>(() =>
            sut.ValidateApiFileEntries("1122205668801257472.json", api)
        );

        Assert.Contains("missing required field 'Request'", ex.Message);
    }
}
