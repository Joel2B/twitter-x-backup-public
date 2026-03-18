namespace Backup.App.Interfaces.Data.Post;

public interface IPostData
{
    public string? Id { get; set; }
    public Task<List<Models.Post.Post>?> GetAll();
    public Task<Dictionary<string, Models.Post.Post>?> GetAllAsDictionary();
    public Task Save(List<Models.Post.Post> posts);
    public Task Prune();
}
