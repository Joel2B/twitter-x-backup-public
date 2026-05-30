using Backup.Infrastructure.Models.Posts.Response;

namespace Backup.Infrastructure.Posts.Adapters.Parsing;

internal static class TweetResultResolver
{
    public static Result Resolve(Entry entry)
    {
        TweetResults tweetResults =
            entry.Content.ItemContent.TweetResults ?? throw new Exception("TweetResults");

        Result result = tweetResults.Result;

        if (result.Tweet is not null)
            result = result.Tweet;

        Result? retweeted = result.Legacy?.RetweetedStatusResult?.Result;

        if (retweeted is null)
            return result;

        result = retweeted;

        if (result.Tweet is not null)
            result = result.Tweet;

        return result;
    }
}
