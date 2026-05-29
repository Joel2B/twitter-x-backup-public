using Backup.Infrastructure.Models.Posts.Response;

namespace Backup.Infrastructure.Posts.Mapping;

public static class HashtagMapper
{
    public static List<string>? Map(Entry entry)
    {
        TweetResults tweetResults =
            entry.Content.ItemContent.TweetResults ?? throw new Exception("tweetResults");

        Result result = tweetResults.Result;
        Entities entities = result.Legacy?.Entities ?? throw new Exception();

        List<string> hashtags =
            entities
                .Hashtags?.Where(hashtag => !string.IsNullOrWhiteSpace(hashtag.Text))
                .Select(hashtag => hashtag.Text)
                .ToList() ?? [];

        if (hashtags.Count == 0)
            return null;

        return hashtags;
    }
}
