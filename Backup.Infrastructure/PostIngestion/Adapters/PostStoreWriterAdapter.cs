using Backup.Application.PostIngestion.Ports;
using Backup.Domain.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.PostIngestion.Adapters;

public class PostStoreWriterAdapter(IPostDomainData postData) : IPostStoreWriter
{
    private readonly IPostDomainData _postData = postData;

    public Task<int> GetCount(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _postData.GetCount();
    }

    public Task AddPosts(
        string userId,
        string origin,
        List<Post> posts,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _postData.AddPosts(userId, origin, posts);
    }

    public Task Save(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _postData.Save();
    }
}
