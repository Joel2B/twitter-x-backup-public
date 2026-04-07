namespace Backup.App.Interfaces.Data.Post;

public interface IPostData
{
    public string? Id { get; set; }
    public Task<List<Models.Post.Post>?> GetAll();
    public Task<List<Models.Post.MediaInput>?> GetMediaInputs();
    public Task<Dictionary<string, Models.Post.Post>?> GetAllAsDictionary();
    public Task<Dictionary<string, Models.Post.Post>> AddPosts(
        string userId,
        string origin,
        List<Models.Post.Post> incoming,
        Models.Post.MergeOptions? options = null
    );
    public Task Save(List<Models.Post.Post> posts);
    public Task Prune();
}
