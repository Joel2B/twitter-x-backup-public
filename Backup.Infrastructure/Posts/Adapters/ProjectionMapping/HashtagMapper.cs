using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Adapters.ProjectionMapping;

public static class HashtagMapper
{
    public static List<string>? Map(Result result)
    {
        Entities entities =
            result.Legacy?.Entities
            ?? throw new FormatException("Tweet payload is missing hashtag entities.");

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
