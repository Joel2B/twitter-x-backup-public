using Backup.App.Api.Controllers;
using Backup.App.Api.Services;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Services.Posts;
using Backup.App.Models.Posts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Tests;

public partial class PostsApiIntegrationTests
{
    private static Post CreatePost(string id, string profileId) =>
        new()
        {
            Id = id,
            Profile = new PostProfile { Id = profileId, UserName = profileId },
            Description = "test",
            Retweeted = false,
            Favorited = false,
            Bookmarked = false,
            CreatedAt = "Sun May 24 04:00:00 +0000 2026",
            Deleted = false,
        };

    private sealed class TestApiHost(HttpClient client, WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<TestApiHost> StartAsync(IPostData postData, IPostParser postParser)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(PostsController).Assembly);
            builder.Services.AddSingleton(postData);
            builder.Services.AddSingleton(postParser);
            builder.Services.AddScoped<IPostIngestionService, PostIngestionService>();
            builder.Services.AddLogging();

            WebApplication app = builder.Build();
            app.MapControllers();
            await app.StartAsync();

            HttpClient client = app.GetTestClient();
            return new TestApiHost(client, app);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class FakePostParser : IPostParser
    {
        public ParseResult NextResult { get; set; } = new([], null);

        public List<(string UserId, string Origin, string ResponseBody)> ParseCalls { get; } = [];

        public ParseResult Parse(string userId, string origin, string response)
        {
            ParseCalls.Add((userId, origin, response));
            return NextResult;
        }

        public ParseUser ParseUser(string response) => new(null);
    }

    private sealed class FakePostData : IPostData
    {
        public string? Id { get; set; }

        public int SaveCalls { get; private set; }

        public List<(string UserId, string Origin, List<Post> Posts)> AddCalls { get; } = [];

        public Task<int> GetCount() => Task.FromResult(0);

        public Task<List<Post>?> GetAll() => Task.FromResult<List<Post>?>([]);

        public Task<List<MediaInput>?> GetMediaInputs() => Task.FromResult<List<MediaInput>?>([]);

        public Task<Dictionary<string, string>> GetHashesById() =>
            Task.FromResult(new Dictionary<string, string>());

        public Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids) =>
            Task.FromResult(new List<Post>());

        public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
            IReadOnlyCollection<string> profileIds
        ) => Task.FromResult(new Dictionary<string, int>());

        public Task AddPosts(
            string userId,
            string origin,
            List<Post> incoming,
            MergeOptions? options = null
        )
        {
            AddCalls.Add((userId, origin, incoming.Select(post => post.Clone()).ToList()));
            return Task.CompletedTask;
        }

        public Task<int> MarkDeletedExcept(
            string userId,
            string origin,
            IReadOnlyCollection<string> keepPostIds
        ) => Task.FromResult(0);

        public Task Reset(List<Post> posts) => Task.CompletedTask;

        public Task UpsertPosts(List<Post> posts) => Task.CompletedTask;

        public Task Save()
        {
            SaveCalls++;
            return Task.CompletedTask;
        }

        public Task Prune() => Task.CompletedTask;
    }
}
