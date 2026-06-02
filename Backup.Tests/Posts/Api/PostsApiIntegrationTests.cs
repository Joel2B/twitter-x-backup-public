using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Tests;

public partial class PostsApiIntegrationTests
{
    [Fact]
    public async Task SaveProcessed_ApiKeyEnabled_MissingHeader_ReturnsUnauthorized()
    {
        FakePostData fakePostData = new();
        IReadOnlyDictionary<string, string?> config = new Dictionary<string, string?>
        {
            ["Backup:Api:Auth:Enabled"] = "true",
            ["Backup:Api:Auth:ApiKey"] = "secret-key",
        };
        await using TestApiHost host = await TestApiHost.StartAsync(
            fakePostData,
            new FakePostParser(),
            config
        );

        var payload = new[]
        {
            new
            {
                id = "p1",
                profile = new { id = "u1", userName = "user1" },
                description = "hello world",
                retweeted = false,
                favorited = false,
                bookmarked = false,
                createdAt = "Sun May 24 04:00:00 +0000 2026",
            },
        };

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/posts/processed?userId=123&origin=extension-search-timeline",
            payload
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(fakePostData.AddCalls);
    }

    [Fact]
    public async Task SaveProcessed_ApiKeyEnabled_ValidHeader_ReturnsOk()
    {
        FakePostData fakePostData = new();
        IReadOnlyDictionary<string, string?> config = new Dictionary<string, string?>
        {
            ["Backup:Api:Auth:Enabled"] = "true",
            ["Backup:Api:Auth:ApiKey"] = "secret-key",
        };
        await using TestApiHost host = await TestApiHost.StartAsync(
            fakePostData,
            new FakePostParser(),
            config
        );

        var payload = new[]
        {
            new
            {
                id = "p1",
                profile = new { id = "u1", userName = "user1" },
                description = "hello world",
                retweeted = false,
                favorited = false,
                bookmarked = false,
                createdAt = "Sun May 24 04:00:00 +0000 2026",
            },
        };

        HttpRequestMessage request = new(
            HttpMethod.Post,
            "/api/posts/processed?userId=123&origin=extension-search-timeline"
        )
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("X-Api-Key", "secret-key");

        HttpResponseMessage response = await host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(fakePostData.AddCalls);
    }

    [Fact]
    public async Task SaveProcessed_ValidPayload_ReturnsOk_AndStoresInFake()
    {
        FakePostData fakePostData = new();
        await using TestApiHost host = await TestApiHost.StartAsync(
            fakePostData,
            new FakePostParser()
        );

        var payload = new[]
        {
            new
            {
                id = "p1",
                profile = new
                {
                    id = "u1",
                    userName = "user1",
                    name = "User One",
                    imageUrl = "https://img.local/u1.jpg",
                    following = false,
                },
                description = "hello world",
                retweeted = false,
                favorited = false,
                bookmarked = false,
                createdAt = "Sun May 24 04:00:00 +0000 2026",
                hashtags = new[] { "test" },
                medias = new[]
                {
                    new
                    {
                        id = "m1",
                        url = "https://img.local/m1.jpg",
                        type = "photo",
                        videoInfo = (object?)null,
                    },
                },
                deleted = false,
            },
        };

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/posts/processed?userId=123&origin=extension-search-timeline",
            payload
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("receivedPosts").GetInt32());
        Assert.Equal(1, body.GetProperty("savedPosts").GetInt32());

        Assert.Single(fakePostData.AddCalls);
        Assert.Equal("123", fakePostData.AddCalls[0].UserId);
        Assert.Equal("extension-search-timeline", fakePostData.AddCalls[0].Origin);
        Assert.Single(fakePostData.AddCalls[0].Posts);
        Assert.Equal("p1", fakePostData.AddCalls[0].Posts[0].Id);
        Assert.Equal(1, fakePostData.SaveCalls);
    }

    [Fact]
    public async Task SaveProcessed_InvalidPayload_ReturnsBadRequest_AndDoesNotStore()
    {
        FakePostData fakePostData = new();
        await using TestApiHost host = await TestApiHost.StartAsync(
            fakePostData,
            new FakePostParser()
        );

        var payload = new[]
        {
            new
            {
                id = "p1",
                profile = new { id = "u1" },
                description = "",
                retweeted = false,
                favorited = false,
                bookmarked = false,
                createdAt = "Sun May 24 04:00:00 +0000 2026",
            },
        };

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/posts/processed?userId=123&origin=extension-search-timeline",
            payload
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(fakePostData.AddCalls);
        Assert.Equal(0, fakePostData.SaveCalls);
    }

    [Fact]
    public async Task SaveRaw_ValidPayload_UsesParser_AndStoresInFake()
    {
        FakePostData fakePostData = new();
        FakePostParser fakeParser = new()
        {
            NextResult = new ParsedPostBatch(
                [CreateParsedPost("r1", "u1"), CreateParsedPost("r2", "u2")],
                "CURSOR_123"
            ),
        };

        await using TestApiHost host = await TestApiHost.StartAsync(fakePostData, fakeParser);

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/posts/raw?userId=42&origin=extension-search-timeline",
            new { data = new { timeline = new { instructions = Array.Empty<object>() } } }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("receivedPosts").GetInt32());
        Assert.Equal(2, body.GetProperty("savedPosts").GetInt32());
        Assert.Equal("CURSOR_123", body.GetProperty("nextCursor").GetString());

        var parseCall = Assert.Single(fakeParser.ParseCalls);
        Assert.Equal("42", parseCall.UserId);
        Assert.Equal("extension-search-timeline", parseCall.Origin);
        Assert.Contains("timeline", parseCall.ResponseBody);

        Assert.Single(fakePostData.AddCalls);
        Assert.Equal(2, fakePostData.AddCalls[0].Posts.Count);
        Assert.Equal(1, fakePostData.SaveCalls);
    }

    [Fact]
    public async Task SaveRaw_MissingUserId_ReturnsBadRequest()
    {
        FakePostData fakePostData = new();
        await using TestApiHost host = await TestApiHost.StartAsync(
            fakePostData,
            new FakePostParser()
        );

        HttpResponseMessage response = await host.Client.PostAsJsonAsync(
            "/api/posts/raw?origin=extension-search-timeline",
            new { data = new { } }
        );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(fakePostData.AddCalls);
        Assert.Equal(0, fakePostData.SaveCalls);
    }
}
