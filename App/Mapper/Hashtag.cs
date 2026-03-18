using Backup.App.Models.Post.Response;

namespace Backup.App.Mapper;

public class Hashtag
{
    public static List<string>? GetHashtags(Entry entry)
    {
        TweetResults tweetResults =
            entry.Content.ItemContent.TweetResults ?? throw new Exception("tweetResults");

        Result result = tweetResults.Result;
        Entities entities = result.Legacy?.Entities ?? throw new Exception();

        List<string> hashtags = [.. entities.Hashtags.Select(o => o.Text)];

        if (hashtags.Count == 0)
            return null;

        return hashtags;
    }
}
