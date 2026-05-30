using Backup.Application.Posts;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;
using DPosts = Backup.Domain.Posts;

namespace Backup.Infrastructure.Services.Posts;

public class PostService(
    ILogger<PostService> _logger,
    IPostExecutionService postExecutionService,
    IPostDomainData _postData,
    IPostRecovery _postRecovery,
    IPostDownload _postDownload
) : IPostService
{
    private readonly ILogger<PostService> _logger = _logger;
    private readonly IPostExecutionService _postExecutionService = postExecutionService;
    private readonly IPostDomainData _postData = _postData;
    private readonly IPostRecovery _postRecovery = _postRecovery;
    private readonly IPostDownload _postDownload = _postDownload;

    public async Task Recover(UsersContext context)
    {
        IPostDomainData data = _postData;

        _logger.LogInformation(data.Id, "post data: {name}", data.GetType().Name);
        _logger.LogInformation(data.Id, "recovering posts in {data}", data.GetType().Name);

        await _postExecutionService.Recover(
            new RecoveryExecution(_postRecovery, data, context)
        );
    }

    public async Task Download(ApiContext context)
    {
        IPostDomainData data = _postData;

        _logger.LogInformation(data.Id, "downloading posts and pruning");
        await _postExecutionService.Download(
            new DownloadExecution(_postDownload, data, context)
        );
    }

    private sealed class RecoveryExecution(
        IPostRecovery recovery,
        IPostDomainData data,
        UsersContext context
    ) : IPostRecoveryExecution
    {
        public Task Recover() => recovery.Recovery(new LegacyPostDataAdapter(data), context);
    }

    private sealed class DownloadExecution(
        IPostDownload download,
        IPostDomainData data,
        ApiContext context
    ) : IPostDownloadExecution
    {
        public Task Download() => download.Download(new LegacyPostDataAdapter(data), context);

        public Task Prune() => data.Prune();
    }

    private sealed class LegacyPostDataAdapter(IPostDomainData source) : IPostData
    {
        private readonly IPostDomainData _source = source;

        public string? Id
        {
            get => _source.Id;
            set => _source.Id = value;
        }

        public Task<int> GetCount() => _source.GetCount();

        public async Task<List<Backup.Infrastructure.Models.Posts.Post>?> GetAll()
        {
            List<DPosts.Post>? posts = await _source.GetAll();
            return posts?.Select(PostReplicationMapper.ToApp).ToList();
        }

        public async Task<List<Backup.Infrastructure.Models.Posts.MediaInput>?> GetMediaInputs()
        {
            List<DPosts.MediaInput>? inputs = await _source.GetMediaInputs();
            return inputs?.Select(PostReplicationMapper.ToApp).ToList();
        }

        public Task<Dictionary<string, string>> GetHashesById() => _source.GetHashesById();

        public async Task<List<Backup.Infrastructure.Models.Posts.Post>> GetByIds(
            IReadOnlyCollection<string> ids
        )
        {
            List<DPosts.Post> posts = await _source.GetByIds(ids);
            return posts.Select(PostReplicationMapper.ToApp).ToList();
        }

        public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
            IReadOnlyCollection<string> profileIds
        ) => _source.GetPostCountsByProfileIds(profileIds);

        public Task AddPosts(
            string userId,
            string origin,
            List<Backup.Infrastructure.Models.Posts.Post> incoming,
            Backup.Infrastructure.Models.Posts.MergeOptions? options = null
        ) =>
            _source.AddPosts(
                userId,
                origin,
                incoming.Select(PostReplicationMapper.ToDomain).ToList(),
                options is null ? null : new DPosts.MergeOptions { Index = options.Index }
            );

        public Task<int> MarkDeletedExcept(
            string userId,
            string origin,
            IReadOnlyCollection<string> keepPostIds
        ) => _source.MarkDeletedExcept(userId, origin, keepPostIds);

        public Task Reset(List<Backup.Infrastructure.Models.Posts.Post> posts) =>
            _source.Reset(posts.Select(PostReplicationMapper.ToDomain).ToList());

        public Task UpsertPosts(List<Backup.Infrastructure.Models.Posts.Post> posts) =>
            _source.UpsertPosts(posts.Select(PostReplicationMapper.ToDomain).ToList());

        public Task Save() => _source.Save();
        public Task Prune() => _source.Prune();
    }
}


