using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config.Data;
using Backup.Infrastructure.Models.Config.Data.Posts;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data;

public partial class SqlitePostData(
    ILogger<SqlitePostData> logger,
    StoragePost config,
    IPartition partition
) : IPostDataStore, ISetup, IAsyncDisposable
{
    public string? Id { get; set; }
    public bool IsDefault { get; set; }

    private readonly ILogger<SqlitePostData> _logger = logger;
    private readonly StoragePost _config = config;
    private readonly IPartition _partition = partition;
    private PostsDbContext? _db;
    private const int SqlInChunkSize = 5000;

    public async Task Setup()
    {
        foreach (PartitionConfig p in _partition.GetPartitions())
            Directory.CreateDirectory(GetBasePath(p));

        await EnsureSchema();
    }

    public async Task AddPosts(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions? options = null
    ) => await AddPostsInternal(userId, origin, incoming, options ?? new());

    public async Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    ) => await MarkDeletedExceptInternal(userId, origin, keepPostIds);

    public Task Reset(List<Post> posts)
    {
        return ResetInternal(posts);
    }

    public async Task UpsertPosts(List<Post> posts) => await UpsertPostsInternal(posts);

    public async Task Save() => await SaveInternal();

    public Task Prune() => PruneInternal();

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
            _db = null;
        }

        GC.SuppressFinalize(this);
    }
}
