using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Adapters.ProjectionMapping;

public static class PostMapper
{
    public static ParsedPostProjection Map(Entry entry)
    {
        TweetResults tweetResults =
            entry.Content.ItemContent.TweetResults
            ?? throw new FormatException("Tweet payload is missing tweet results.");

        Result result = PostResultResolutionPolicy.ResolvePrimaryThenRetweeted(
            tweetResults.Result,
            current => current.Tweet,
            current => current.Legacy?.RetweetedStatusResult?.Result
        );

        Legacy legacy =
            result.Legacy ?? throw new FormatException("Tweet payload is missing legacy data.");

        ParsedPostProfileProjection profile = ProfileMapper.Map(result);
        List<ParsedPostMediaProjection>? media = MediaMapper.Map(result);

        return new()
        {
            Id = legacy.IdStr ?? throw new FormatException("Tweet payload is missing id."),
            Profile = profile,
            Description =
                legacy.FullText ?? throw new FormatException("Tweet payload is missing full text."),
            Retweeted = legacy.Retweeted,
            Favorited = legacy.Favorited,
            Bookmarked = legacy.Bookmarked ?? false,
            CreatedAt =
                legacy.CreatedAt
                ?? throw new FormatException("Tweet payload is missing created_at."),
            Hashtags = HashtagMapper.Map(result),
            Medias = media,
        };
    }
}
