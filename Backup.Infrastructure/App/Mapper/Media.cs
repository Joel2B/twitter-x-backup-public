using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Models.Posts.Response;
using Newtonsoft.Json.Linq;

namespace Backup.Infrastructure.Mapper;

public class Media
{
    public static List<PostMedia>? GetMedias(Entry entry)
    {
        Result result = entry.Content.ItemContent.TweetResults!.Result;

        List<Medium>? _media = result.Legacy!.Entities.Media;
        _media ??= GetMediaCard(result);

        if (_media is null)
            return null;

        List<PostMedia>? media = _media
            .Select(o => new PostMedia
            {
                Id = o.IdStr,
                Type = o.Type,
                Url = o.MediaUrlHttps,
                VideoInfo = GetVideoInfo(o),
            })
            .ToList();

        return media;
    }

    private static List<Medium>? GetMediaCard(Result result)
    {
        if (result.Card is null)
            return null;

        List<Binding>? bindings = result
            .Card.LegacyCard?.BindingValues?.Where(o => o.Key == "unified_card")
            .ToList();

        if (bindings is null || bindings.Count == 0)
            return null;

        if (bindings.Count > 1)
            throw new Exception();

        string value = bindings.Select(o => o.Value.StringValue).First();
        JObject root = JObject.Parse(value);
        string? key = root.SelectToken("component_objects.media_1.data.id")?.ToString();

        List<string?> ids = root.SelectTokens("$..swipeable_media_1.data.media_list[*].id")
            .Values<string>()
            .ToList();

        if (key is null && ids.Count == 0)
            throw new Exception();

        List<Medium> media = [];

        if (key is not null)
            ids.Add(key);

        foreach (string? id in ids)
        {
            if (id is null)
                continue;

            Medium? mediaCard = root.SelectToken($"media_entities['{id}']")?.ToObject<Medium>();

            if (mediaCard is null)
                throw new Exception();

            media.Add(mediaCard);
        }

        return media;
    }

    private static PostVideoInfo? GetVideoInfo(Medium medium)
    {
        if (medium.VideoInfo is null)
            return null;

        PostVideoInfo videoInfo = new()
        {
            DurationMilis = medium.VideoInfo.DurationMillis,
            Variants = medium
                .VideoInfo.Variants.Select(medium => new PostVariant
                {
                    Bitrate = medium.Bitrate,
                    ContentType = medium.ContentType,
                    Url = medium.Url,
                })
                .ToList(),
        };

        return videoInfo;
    }
}

