using Backup.Domain.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Adapters;
using AppPosts = Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Data;

public partial class LocalPostData : IPostDomainDataStore
{
    bool IPostDomainDataStore.IsDefault => IsDefault;

    async Task<List<Post>?> IPostDomainData.GetAll()
    {
        List<AppPosts.Post>? posts = await GetAll();
        return posts?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    async Task<List<MediaInput>?> IPostDomainData.GetMediaInputs()
    {
        List<AppPosts.MediaInput>? inputs = await GetMediaInputs();
        return inputs?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    async Task<List<Post>> IPostDomainData.GetByIds(IReadOnlyCollection<string> ids)
    {
        List<AppPosts.Post> posts = await GetByIds(ids);
        return posts.Select(PostReplicationMapper.ToDomain).ToList();
    }

    Task IPostDomainData.AddPosts(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions? options
    ) =>
        AddPosts(
            userId,
            origin,
            incoming.Select(PostReplicationMapper.ToApp).ToList(),
            PostReplicationMapper.ToApp(options)
        );

    Task IPostDomainData.Reset(List<Post> posts) =>
        Reset(posts.Select(PostReplicationMapper.ToApp).ToList());

    Task IPostDomainData.UpsertPosts(List<Post> posts) =>
        UpsertPosts(posts.Select(PostReplicationMapper.ToApp).ToList());

    async Task<PostStoreCounts> IPostDomainDataStore.GetStoreCounts()
    {
        AppPosts.PostStoreCounts counts = await GetStoreCounts();
        return PostReplicationMapper.ToDomain(counts);
    }
}
