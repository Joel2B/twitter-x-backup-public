using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Adapters.ProjectionMapping;

public static class PostMapper
{
    public static ParsedPostProjection Map(Entry entry)
    {
        TweetResults tweetResults =
            entry.Content.ItemContent.TweetResults ?? throw new Exception("TweetResults");

        Result result = PostResultResolutionPolicy.ResolvePrimaryThenRetweeted(
            tweetResults.Result,
            current => current.Tweet,
            current => current.Legacy?.RetweetedStatusResult?.Result
        );

        Legacy legacy = result.Legacy ?? throw new Exception("Legacy");
        ParsedPostProfileProjection profile = ProfileMapper.Map(result);
        List<ParsedPostMediaProjection>? media = MediaMapper.Map(result);

        return new()
        {
            Id = legacy.IdStr ?? throw new Exception("Id"),
            Profile = profile,
            Description = legacy.FullText ?? throw new Exception("Description"),
            Retweeted = legacy.Retweeted,
            Favorited = legacy.Favorited,
            Bookmarked = legacy.Bookmarked ?? false,
            CreatedAt = legacy.CreatedAt ?? throw new Exception("CreatedAt"),
            Hashtags = HashtagMapper.Map(result),
            Medias = media,
        };
    }
}
