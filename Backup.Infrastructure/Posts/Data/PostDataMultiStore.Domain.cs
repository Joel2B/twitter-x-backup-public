using Backup.Domain.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Adapters;

namespace Backup.Infrastructure.Posts.Data;

public partial class PostDataMultiStore : IPostDomainData
{
    string? IPostDomainData.Id
    {
        get => Id;
        set => Id = value;
    }

    Task<int> IPostDomainData.GetCount() => GetCount();

    async Task<List<Post>?> IPostDomainData.GetAll()
    {
        List<Backup.Infrastructure.Posts.Models.Post>? posts = await GetAll();
        return posts?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    async Task<List<MediaInput>?> IPostDomainData.GetMediaInputs()
    {
        List<Backup.Infrastructure.Posts.Models.MediaInput>? inputs = await GetMediaInputs();
        return inputs?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    Task<Dictionary<string, string>> IPostDomainData.GetHashesById() => GetHashesById();

    async Task<List<Post>> IPostDomainData.GetByIds(IReadOnlyCollection<string> ids)
    {
        List<Backup.Infrastructure.Posts.Models.Post> posts = await GetByIds(ids);
        return posts.Select(PostReplicationMapper.ToDomain).ToList();
    }

    Task<Dictionary<string, int>> IPostDomainData.GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    ) => GetPostCountsByProfileIds(profileIds);

    Task IPostDomainData.AddPosts(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions? options
    ) => AddPosts(userId, origin, incoming.Select(PostReplicationMapper.ToApp).ToList(), PostReplicationMapper.ToApp(options));

    Task<int> IPostDomainData.MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    ) => MarkDeletedExcept(userId, origin, keepPostIds);

    Task IPostDomainData.Reset(List<Post> posts) =>
        Reset(posts.Select(PostReplicationMapper.ToApp).ToList());

    Task IPostDomainData.UpsertPosts(List<Post> posts) =>
        UpsertPosts(posts.Select(PostReplicationMapper.ToApp).ToList());

    Task IPostDomainData.Save() => Save();

    Task IPostDomainData.Prune() => Prune();
}
