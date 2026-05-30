using Backup.Domain.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Posts.Adapters;

public sealed class PostDataDomainAdapter(IPostData postData) : IPostDomainData
{
    private readonly IPostData _postData = postData;

    public string? Id
    {
        get => _postData.Id;
        set => _postData.Id = value;
    }

    public Task<int> GetCount() => _postData.GetCount();

    public async Task<List<Post>?> GetAll()
    {
        List<Backup.Infrastructure.Models.Posts.Post>? posts = await _postData.GetAll();
        return posts?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public async Task<List<MediaInput>?> GetMediaInputs()
    {
        List<Backup.Infrastructure.Models.Posts.MediaInput>? inputs = await _postData.GetMediaInputs();
        return inputs?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public Task<Dictionary<string, string>> GetHashesById() => _postData.GetHashesById();

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        List<Backup.Infrastructure.Models.Posts.Post> posts = await _postData.GetByIds(ids);
        return posts.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(IReadOnlyCollection<string> profileIds) =>
        _postData.GetPostCountsByProfileIds(profileIds);

    public Task AddPosts(string userId, string origin, List<Post> incoming, MergeOptions? options = null) =>
        _postData.AddPosts(
            userId,
            origin,
            incoming.Select(PostReplicationMapper.ToApp).ToList(),
            PostReplicationMapper.ToApp(options)
        );

    public Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    ) => _postData.MarkDeletedExcept(userId, origin, keepPostIds);

    public Task Reset(List<Post> posts) =>
        _postData.Reset(posts.Select(PostReplicationMapper.ToApp).ToList());

    public Task UpsertPosts(List<Post> posts) =>
        _postData.UpsertPosts(posts.Select(PostReplicationMapper.ToApp).ToList());

    public Task Save() => _postData.Save();
    public Task Prune() => _postData.Prune();
}
