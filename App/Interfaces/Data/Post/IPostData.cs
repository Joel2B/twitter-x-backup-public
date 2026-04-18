namespace Backup.App.Interfaces.Data.Post;

public interface IPostData
{
    public string? Id { get; set; }
    public Task<int> GetCount();
    public Task<List<Models.Post.Post>?> GetAll();
    public Task<List<Models.Post.MediaInput>?> GetMediaInputs();
    public Task<Dictionary<string, Models.Post.Post>?> GetAllAsDictionary();
    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    );
    public Task AddPosts(
        string userId,
        string origin,
        List<Models.Post.Post> incoming,
        Models.Post.MergeOptions? options = null
    );
    public Task Reset(List<Models.Post.Post> posts);
    public Task Save();
    public Task Prune();
}
