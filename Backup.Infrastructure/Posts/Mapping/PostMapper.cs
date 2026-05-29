using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Models.Posts.Response;

namespace Backup.Infrastructure.Posts.Mapping;

public static class PostMapper
{
    public static Post Map(Entry entry)
    {
        NormalizeResult(entry);

        Legacy legacy =
            entry.Content.ItemContent.TweetResults?.Result.Legacy ?? throw new Exception("Legacy");

        return new()
        {
            Id = legacy.IdStr ?? throw new Exception("Id"),
            Profile = ProfileMapper.Map(entry),
            Description = legacy.FullText ?? throw new Exception("Description"),
            Retweeted = legacy.Retweeted,
            Favorited = legacy.Favorited,
            Bookmarked = legacy.Bookmarked ?? false,
            CreatedAt = legacy.CreatedAt ?? throw new Exception("CreatedAt"),
            Hashtags = HashtagMapper.Map(entry),
            Medias = MediaMapper.Map(entry),
        };
    }

    private static void NormalizeResult(Entry entry)
    {
        TweetResults? tweetResults = entry.Content.ItemContent.TweetResults;

        if (tweetResults is null)
            throw new Exception("TweetResults");

        Result result = tweetResults.Result;

        if (result.Tweet is not null)
            tweetResults.Result = result.Tweet;

        Result? retweeted = result.Legacy?.RetweetedStatusResult?.Result;

        if (retweeted is null)
            return;

        tweetResults.Result = retweeted;

        if (tweetResults.Result.Tweet is not null)
            tweetResults.Result = tweetResults.Result.Tweet;
    }
}
