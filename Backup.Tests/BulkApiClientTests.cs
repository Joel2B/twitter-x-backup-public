using Backup.Domain.Posts;
using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.Interfaces.Services.Bulk;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Bulk;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;
using Microsoft.Extensions.Logging.Abstractions;
using ParseResult = Backup.Domain.Posts.ParseResult;
using ParseUser = Backup.Domain.Posts.ParseUser;

namespace Backup.Tests;

public class BulkApiClientTests
{
    [Fact]
    public async Task Verify_DelegatesToDownloader()
    {
        FakeBulkRequestFactory requestFactory = new();
        FakeBulkSourceRouteProvider routeProvider = new();
        FakePostDownloader downloader = new() { VerifyResult = true };
        FakePostDomainParser parser = new();

        BulkApiClient sut = new(
            NullLogger<BulkApiClient>.Instance,
            requestFactory,
            routeProvider,
            downloader,
            parser
        );

        bool result = await sut.Verify();

        Assert.True(result);
        Assert.Equal(1, downloader.VerifyCalls);
    }

    [Fact]
    public async Task GetUserByUser_WhenRequestMissing_ReturnsNull()
    {
        FakeBulkRequestFactory requestFactory = new() { UserByScreenNameRequest = null };
        FakeBulkSourceRouteProvider routeProvider = new();
        FakePostDownloader downloader = new();
        FakePostDomainParser parser = new();

        BulkApiClient sut = new(
            NullLogger<BulkApiClient>.Instance,
            requestFactory,
            routeProvider,
            downloader,
            parser
        );

        ParseUser? result = await sut.GetUserByUser(
            new Dictionary<string, ApiConfig>(),
            "test-user",
            CancellationToken.None
        );

        Assert.Null(result);
        Assert.Equal(0, downloader.DownloadCalls);
        Assert.Equal(0, parser.ParseUserCalls);
    }

    [Fact]
    public async Task GetUserByUser_SetsScreenNameAndReferer_AndParsesResponse()
    {
        Request request = CreateRequest("https://x.com/graphql/user");
        FakeBulkRequestFactory requestFactory = new() { UserByScreenNameRequest = request };
        FakeBulkSourceRouteProvider routeProvider = new() { RefererValue = "https://x.com/notifications" };
        FakePostDownloader downloader = new() { DownloadResponse = "{\"ok\":true}" };
        FakePostDomainParser parser = new()
        {
            ParseUserResult = new ParseUser(new PostUser { Id = "u1", MediaCount = 10 }),
        };

        BulkApiClient sut = new(
            NullLogger<BulkApiClient>.Instance,
            requestFactory,
            routeProvider,
            downloader,
            parser
        );

        ParseUser? result = await sut.GetUserByUser(
            new Dictionary<string, ApiConfig>(),
            "alice",
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.NotNull(result!.User);
        Assert.Equal("u1", result.User!.Id);
        Assert.Equal(10, result.User.MediaCount);
        Assert.Equal("alice", request.Query.Variables["screen_name"]?.ToString());
        Assert.Equal("https://x.com/notifications", request.Headers["Referer"]);
        Assert.Equal(1, downloader.DownloadCalls);
        Assert.Equal(1, parser.ParseUserCalls);
    }

    [Fact]
    public async Task GetUserMedia_SetsVariablesAndReferer_AndParsesResponse()
    {
        Request request = CreateRequest("https://x.com/graphql/media");
        FakeBulkRequestFactory requestFactory = new() { UserMediaRequest = request };
        FakeBulkSourceRouteProvider routeProvider = new() { RefererValue = "https://x.com/notifications" };
        FakePostDownloader downloader = new() { DownloadResponse = "{\"media\":[]}" };
        ParseResult expected = new([], "CURSOR-1");
        FakePostDomainParser parser = new() { ParseResultValue = expected };

        BulkApiClient sut = new(
            NullLogger<BulkApiClient>.Instance,
            requestFactory,
            routeProvider,
            downloader,
            parser
        );

        ParseResult? result = await sut.GetUserMedia(
            new Dictionary<string, ApiConfig>(),
            "uid-1",
            "media",
            33,
            "CURSOR-0",
            CancellationToken.None
        );

        Assert.Same(expected, result);
        Assert.Equal("uid-1", request.Query.Variables["userId"]?.ToString());
        Assert.Equal(33, Convert.ToInt32(request.Query.Variables["count"]));
        Assert.Equal("CURSOR-0", request.Query.Variables["cursor"]?.ToString());
        Assert.Equal("https://x.com/notifications", request.Headers["Referer"]);
        Assert.Equal(1, parser.ParseCalls);
        Assert.Equal("uid-1", parser.LastParseUserId);
        Assert.Equal("media", parser.LastParseOrigin);
        Assert.Equal("{\"media\":[]}", parser.LastParseResponse);
    }

    [Fact]
    public async Task GetUserMedia_WhenDownloaderThrows_ReturnsNull()
    {
        Request request = CreateRequest("https://x.com/graphql/media");
        FakeBulkRequestFactory requestFactory = new() { UserMediaRequest = request };
        FakeBulkSourceRouteProvider routeProvider = new();
        FakePostDownloader downloader = new() { ThrowOnDownload = true };
        FakePostDomainParser parser = new();

        BulkApiClient sut = new(
            NullLogger<BulkApiClient>.Instance,
            requestFactory,
            routeProvider,
            downloader,
            parser
        );

        ParseResult? result = await sut.GetUserMedia(
            new Dictionary<string, ApiConfig>(),
            "uid-1",
            "media",
            20,
            null,
            CancellationToken.None
        );

        Assert.Null(result);
        Assert.Equal(0, parser.ParseCalls);
    }

    private static Request CreateRequest(string url) =>
        new()
        {
            Url = url,
            Query = new Query
            {
                Variables = [],
                Features = [],
                FieldToggles = [],
            },
            Headers = [],
        };

    private sealed class FakeBulkRequestFactory : IBulkRequestFactory
    {
        public Request? UserByScreenNameRequest { get; init; } = null;
        public Request? UserMediaRequest { get; init; } = null;

        public Request? BuildUserByScreenName(IReadOnlyDictionary<string, ApiConfig> api) =>
            UserByScreenNameRequest;

        public Request? BuildUserMedia(IReadOnlyDictionary<string, ApiConfig> api) =>
            UserMediaRequest;
    }

    private sealed class FakeBulkSourceRouteProvider : IBulkSourceRouteProvider
    {
        public string RefererValue { get; init; } = "https://x.com/notifications";

        public string? GetOrigin(SourceType sourceType) => "media";

        public string GetReferer(SourceType sourceType = SourceType.Notifications, string? userName = null) =>
            RefererValue;
    }

    private sealed class FakePostDownloader : IPostDownloader
    {
        public bool VerifyResult { get; init; } = true;
        public bool ThrowOnDownload { get; init; } = false;
        public string DownloadResponse { get; init; } = "";
        public int VerifyCalls { get; private set; }
        public int DownloadCalls { get; private set; }

        public Task<string> Download(Request request, CancellationToken token)
        {
            DownloadCalls++;

            if (ThrowOnDownload)
                throw new InvalidOperationException("download failed");

            return Task.FromResult(DownloadResponse);
        }

        public Task<bool> Verify()
        {
            VerifyCalls++;
            return Task.FromResult(VerifyResult);
        }
    }

    private sealed class FakePostDomainParser : IPostDomainParser
    {
        public ParseUser ParseUserResult { get; init; } = new(null);
        public ParseResult ParseResultValue { get; init; } = new([], null);
        public int ParseUserCalls { get; private set; }
        public int ParseCalls { get; private set; }
        public string? LastParseUserId { get; private set; }
        public string? LastParseOrigin { get; private set; }
        public string? LastParseResponse { get; private set; }

        public ParseResult Parse(string userId, string origin, string response)
        {
            ParseCalls++;
            LastParseUserId = userId;
            LastParseOrigin = origin;
            LastParseResponse = response;
            return ParseResultValue;
        }

        public ParseUser ParseUser(string response)
        {
            ParseUserCalls++;
            return ParseUserResult;
        }
    }
}
