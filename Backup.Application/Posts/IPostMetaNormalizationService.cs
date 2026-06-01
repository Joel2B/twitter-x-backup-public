using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostMetaNormalizationService
{
    IReadOnlyDictionary<string, PostMetaRecord> Normalize(
        IReadOnlyCollection<PostMetaRecord> entries
    );
}
