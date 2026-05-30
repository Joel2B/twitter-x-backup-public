using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public interface IPostProjectionComposer
{
    Post Compose(ParsedPostProjection source);
    List<Post> ComposeMany(IEnumerable<ParsedPostProjection> source);
}
