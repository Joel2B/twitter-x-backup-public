using Backup.App.Interfaces.Data.Post;

namespace Backup.App.Interfaces.Services.Post;

public interface IPostRecovery
{
    public Task Recovery(Dictionary<string, Models.Post.Post> posts, IPostData postData);
}
