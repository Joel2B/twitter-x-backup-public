using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Post;

public class PostReplication(ILogger<PostReplication> _logger) : IPostReplication
{
    private readonly ILogger<PostReplication> _logger = _logger;

    public async Task Replicate(IEnumerable<IPostData> data)
    {
        IPostData source = data.First();

        try
        {
            List<Models.Post.Post>? posts = null;

            foreach (IPostData postData in data.Except([source]))
            {
                posts ??= await source.GetAll();

                if (posts is null)
                    throw new Exception();

                await postData.Save(posts);
                await postData.Prune();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }
    }
}
