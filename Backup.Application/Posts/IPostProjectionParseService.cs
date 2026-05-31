using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public interface IPostProjectionParseService
{
    PostProjectionParseBatchResult Parse<TSource>(
        IEnumerable<TSource> source,
        Func<TSource, ParsedPostProjection> map
    );
}
