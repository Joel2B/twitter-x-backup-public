using Backup.Application.PostIngestion.Ports;
using Backup.App.Interfaces.Data.Posts;
using Backup.Domain.Posts;

namespace Backup.Infrastructure.PostIngestion.Adapters;

public class PostStoreWriterAdapter(IPostData postData) : IPostStoreWriter
{
    private readonly IPostData _postData = postData;

    public Task<int> GetCount() => _postData.GetCount();

    public Task AddPosts(string userId, string origin, List<Post> posts) =>
        _postData.AddPosts(userId, origin, posts.Select(PostDomainMapper.ToApp).ToList());

    public Task Save() => _postData.Save();
}
