using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostProjectionParseService : IPostProjectionParseService
{
    public PostProjectionParseBatchResult Parse<TSource>(
        IEnumerable<TSource> source,
        Func<TSource, ParsedPostProjection> map
    )
    {
        List<ParsedPostProjection> posts = [];
        List<string> errors = [];
        int index = 0;

        foreach (TSource item in source)
        {
            try
            {
                ParsedPostProjection post = map(item);
                posts.Add(post);
            }
            catch (Exception ex)
            {
                errors.Add($"entry[{index}] {ex.Message}");
            }

            index++;
        }

        return new PostProjectionParseBatchResult { Posts = posts, Errors = errors };
    }
}
