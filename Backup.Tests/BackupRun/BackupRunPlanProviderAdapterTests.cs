using Backup.Application.BackupRun;
using Backup.Infrastructure.BackupRun.Adapters;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Backup;
using Backup.Infrastructure.Models.Config.Data.Bulk;
using Backup.Infrastructure.Models.Config.Data.Dump;
using Backup.Infrastructure.Models.Config.Data.Media;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Models.Config.Downloads;
using Backup.Infrastructure.Models.Config.Medias;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Config.Request;
using Backup.Infrastructure.Models.Config.Tasks;

namespace Backup.Tests;

public class BackupRunPlanProviderAdapterTests
{
    [Fact]
    public void GetPlan_BuildsExpectedUsersSourcesAndRunFlags()
    {
        AppConfig config = CreateConfig();
        BackupRunPlanProviderAdapter adapter = new(config, new BackupRunPlanBuilder());

        var plan = adapter.GetPlan();

        Assert.True(plan.IsBulkEnabled);
        Assert.True(plan.IsMediaEnabled);
        Assert.Equal(2, plan.Users.Count);

        var firstUser = plan.Users[0];
        Assert.Equal("user-1", firstUser.UserId);
        Assert.True(firstUser.RunRecovery);
        Assert.True(firstUser.RunBulk);
        Assert.Equal(2, firstUser.Sources.Count);

        var sourceA = firstUser.Sources.First(source => source.SourceId == "Api.SearchTimeline");
        Assert.Equal("SearchTimeline", sourceA.ApiId);
        Assert.Equal(50, sourceA.Count);
        Assert.Equal("https://x.com/graphql/search", sourceA.Request.Url);
        Assert.Equal(20, Convert.ToInt32(sourceA.Request.Variables["count"]));

        var sourceB = firstUser.Sources.First(source => source.SourceId == "Api.UserMedia");
        Assert.Equal("UserMedia", sourceB.ApiId);
        Assert.Equal(25, sourceB.Count);

        var secondUser = plan.Users[1];
        Assert.Equal("user-2", secondUser.UserId);
        Assert.False(secondUser.RunRecovery);
        Assert.False(secondUser.RunBulk);
        Assert.Single(secondUser.Sources);
        Assert.Equal("Api.SearchTimeline", secondUser.Sources[0].SourceId);
    }

    [Fact]
    public void GetPlan_SkipsDisabledOrMissingApisFromSources()
    {
        AppConfig config = CreateConfig();
        config.Fetch["Api.Disabled"] = new FetchItem { Count = 99, Api = 0 };
        config.Fetch["Api.Missing"] = new FetchItem { Count = 88, Api = 0 };
        config.UsersContext[0].Api["Api.Disabled"] = new ApiConfig
        {
            Id = "DisabledApi",
            Enabled = false,
            Request = CreateRequest("https://x.com/graphql/disabled"),
        };

        BackupRunPlanProviderAdapter adapter = new(config, new BackupRunPlanBuilder());
        var plan = adapter.GetPlan();

        var firstUser = plan.Users[0];
        Assert.DoesNotContain(firstUser.Sources, source => source.SourceId == "Api.Disabled");
        Assert.DoesNotContain(firstUser.Sources, source => source.SourceId == "Api.Missing");
    }

    private static AppConfig CreateConfig()
    {
        Dictionary<string, FetchItem> fetch = new()
        {
            ["Api.SearchTimeline"] = new FetchItem { Count = 50, Api = 0 },
            ["Api.UserMedia"] = new FetchItem { Count = 25, Api = 0 },
        };

        List<UsersContext> users =
        [
            new UsersContext
            {
                UserId = "user-1",
                Api = new Dictionary<string, ApiConfig>
                {
                    ["Api.SearchTimeline"] = new ApiConfig
                    {
                        Id = "SearchTimeline",
                        Enabled = true,
                        Request = CreateRequest("https://x.com/graphql/search"),
                    },
                    ["Api.UserMedia"] = new ApiConfig
                    {
                        Id = "UserMedia",
                        Enabled = true,
                        Request = CreateRequest("https://x.com/graphql/media"),
                    },
                },
            },
            new UsersContext
            {
                UserId = "user-2",
                Api = new Dictionary<string, ApiConfig>
                {
                    ["Api.SearchTimeline"] = new ApiConfig
                    {
                        Id = "SearchTimelineV2",
                        Enabled = true,
                        Request = CreateRequest("https://x.com/graphql/search-v2"),
                    },
                },
            },
        ];

        return new AppConfig
        {
            UsersContext = users,
            Fetch = fetch,
            Services = new ServicesConfig
            {
                Recovery = new Recovery { Enabled = true },
                Dump = new DumpService { Count = 1 },
                Users = [],
            },
            Data = CreateDataConfig(),
            Downloads = CreateDownloadsConfig(),
            Medias = CreateMediasConfig(enabled: true),
            Proxy = CreateProxyConfig(),
            Debug = CreateDebugConfig(),
            Tasks = new TasksConfig
            {
                Prune = new Prune
                {
                    Data = new Backup.Infrastructure.Models.Config.Tasks.Data
                    {
                        Post = new PruneConfig { KeepCount = 1, KeepDays = 1 },
                    },
                },
            },
            Bulk = new BulkConfig { Enabled = true },
            Network = new NetworkConfig
            {
                RateLimit = new RateLimit
                {
                    Enabled = true,
                    ThresholdRemaining = 1,
                    Wait = new RateLimitWait
                    {
                        Min = 1,
                        Max = 2,
                        Reset = false,
                    },
                },
            },
        };
    }

    private static Request CreateRequest(string url) =>
        new()
        {
            Url = url,
            Query = new Query
            {
                Variables = new Dictionary<string, object?> { ["count"] = 20, ["cursor"] = "C1" },
                Features = new Dictionary<string, bool> { ["featureA"] = true },
                FieldToggles = new Dictionary<string, bool> { ["fieldA"] = false },
            },
            Headers = new Dictionary<string, string> { ["authorization"] = "Bearer token" },
        };

    private static DataConfig CreateDataConfig() =>
        new()
        {
            Aliases = [],
            Partitions = [],
            Post =
            [
                new StoragePost
                {
                    Id = "p",
                    Default = true,
                    Type = "json",
                    Enabled = true,
                    Partitions = [],
                    Tasks = new Backup.Infrastructure.Models.Config.Data.Tasks { Prune = true },
                    Paths = new Backup.Infrastructure.Models.Config.Data.Posts.Paths
                    {
                        Paths = ["./data"],
                        Post = new PathConfig { Paths = ["./data/post"] },
                    },
                },
            ],
            Dump =
            [
                new StorageDump
                {
                    Id = "d",
                    Default = true,
                    Type = "json",
                    Enabled = true,
                    Partitions = [],
                    Tasks = new Backup.Infrastructure.Models.Config.Data.Tasks { Prune = true },
                    Paths = new Backup.Infrastructure.Models.Config.Data.Dump.Paths
                    {
                        Paths = ["./dump"],
                        Dumps = new Dumps
                        {
                            Paths = ["./dump/dumps"],
                            Dump = new Dump
                            {
                                Paths = ["./dump/dumps/dump"],
                                Api = new PathConfig { Paths = ["./dump/dumps/dump/api"] },
                            },
                        },
                    },
                },
            ],
            Bulk =
            [
                new StorageBulk
                {
                    Id = "b",
                    Default = true,
                    Type = "json",
                    Enabled = true,
                    Partitions = [],
                    Tasks = new Backup.Infrastructure.Models.Config.Data.Tasks { Prune = true },
                    Paths = new Backup.Infrastructure.Models.Config.Data.Bulk.Paths
                    {
                        Paths = ["./bulk"],
                        Bulk = new PathConfig { Paths = ["./bulk/files"] },
                        Sources = new PathConfig { Paths = ["./bulk/sources"] },
                    },
                },
            ],
            Media =
            [
                new StorageMedia
                {
                    Id = "m",
                    Default = true,
                    Type = "json",
                    Enabled = true,
                    Partitions = [],
                    Tasks = new Backup.Infrastructure.Models.Config.Data.Tasks { Prune = true },
                    Paths = new Backup.Infrastructure.Models.Config.Data.Media.Paths
                    {
                        Paths = ["./media"],
                        Media = new PathConfig { Paths = ["./media/files"] },
                        Cache = new PathConfig { Paths = ["./media/cache"] },
                        Tmp = new Tmp
                        {
                            Paths = ["./media/tmp"],
                            Downloader = new PathConfig { Paths = ["./media/tmp/downloader"] },
                            Downloaded = new PathConfig { Paths = ["./media/tmp/downloaded"] },
                        },
                    },
                },
            ],
            Backup =
            [
                new StorageBackup
                {
                    Id = "backup",
                    Default = true,
                    Type = "json",
                    Enabled = true,
                    Partitions = [],
                    Tasks = new Backup.Infrastructure.Models.Config.Data.Tasks { Prune = true },
                    Paths = new Backup.Infrastructure.Models.Config.Data.Backup.Paths
                    {
                        Paths = ["./backup"],
                        Cache = new PathConfig { Paths = ["./backup/cache"] },
                    },
                    Chunk = new ChunkConfig
                    {
                        Paths = ["./backup/chunk"],
                        Path = new PathChunkConfig
                        {
                            Paths = ["./backup/chunk/path"],
                            Size = 1,
                            Increase = 1,
                        },
                        Data = new PathConfig { Paths = ["./backup/chunk/data"] },
                        Zip = new PathConfig { Paths = ["./backup/chunk/zip"] },
                    },
                    Direct = new Direct { Paths = ["./backup/direct"] },
                },
            ],
        };

    private static DownloadsConfig CreateDownloadsConfig() =>
        new()
        {
            Enabled = true,
            Threads = new Threads
            {
                Start = 1,
                Min = 1,
                Max = 2,
            },
            Count = 1,
            MaxBytesPerSecond = 1,
            Prune = new Filter { Filters = [] },
            Media = new MediaDebug
            {
                Paths = ["./debug/media"],
                Partitions = [],
                Log = new PathConfig { Paths = ["./debug/media/log"] },
                Error = new PathConfig { Paths = ["./debug/media/error"] },
            },
        };

    private static MediasConfig CreateMediasConfig(bool enabled) =>
        new()
        {
            Enabled = enabled,
            Banner = new BannerConfig { Filters = [] },
            Profile = new ProfileConfig { Filters = [] },
            Photo = new PhotoConfig { Filters = [] },
            Video = new VideoConfig
            {
                Filters = [],
                Thumb = new MediaConfig { Filters = [] },
            },
            Gif = new GifConfig
            {
                Filters = [],
                Thumb = new MediaConfig { Filters = [] },
            },
        };

    private static ProxyConfig CreateProxyConfig() =>
        new()
        {
            Enabled = false,
            Check = false,
            Partitions = [],
            Data = new Backup.Infrastructure.Models.Config.Proxy.Data
            {
                Paths = ["./proxy"],
                Proxy = new PathConfig { Paths = ["./proxy/data"] },
            },
            Threshold = new Threshold { ErrorsToInactive = 1, ErrorsToStop = 1 },
            Providers = [],
        };

    private static DebugConfig CreateDebugConfig() =>
        new()
        {
            Paths = ["./debug"],
            Partitions = [],
            Log = new PathConfig { Paths = ["./debug/log"] },
            Api = new DebugApi
            {
                Paths = ["./debug/api"],
                Prune = new DebugPrune { Enabled = false, RetainedCountLimit = 1 },
            },
        };
}
