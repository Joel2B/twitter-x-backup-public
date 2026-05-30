using Backup.Application.PostIngestion.Ports;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Domain.Posts;

namespace Backup.Infrastructure.PostIngestion.Adapters;

public class PostStoreWriterAdapter(IPostDomainData postData) : IPostStoreWriter
{
    private readonly IPostDomainData _postData = postData;

    public Task<int> GetCount() => _postData.GetCount();

    public Task AddPosts(string userId, string origin, List<Post> posts) =>
        _postData.AddPosts(userId, origin, posts);

    public Task Save() => _postData.Save();
}
