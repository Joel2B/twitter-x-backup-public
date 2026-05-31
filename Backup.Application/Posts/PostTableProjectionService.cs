using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostTableProjectionService : IPostTableProjectionService
{
    public PostTableProjectionResult Project(IReadOnlyList<Backup.Domain.Posts.Post> posts)
    {
        List<PostTablePostRow> postRows = [];
        List<PostTableHashtagRow> hashtagRows = [];
        List<PostTableMediaRow> mediaRows = [];
        List<PostTableMediaVariantRow> variantRows = [];
        List<PostTableIndexEntryRow> indexRows = [];
        Dictionary<string, PostTableProfileRow> profileById = new(StringComparer.Ordinal);

        foreach (Backup.Domain.Posts.Post post in posts)
        {
            profileById[post.Profile.Id] = new PostTableProfileRow
            {
                Id = post.Profile.Id,
                UserName = post.Profile.UserName,
                Name = post.Profile.Name,
                BannerUrl = post.Profile.BannerUrl,
                ImageUrl = post.Profile.ImageUrl,
                Following = post.Profile.Following,
                CountMedia = post.Profile.Count?.Media,
            };

            postRows.Add(
                new PostTablePostRow
                {
                    Id = post.Id,
                    ProfileId = post.Profile.Id,
                    Description = post.Description,
                    Retweeted = post.Retweeted,
                    Favorited = post.Favorited,
                    Bookmarked = post.Bookmarked,
                    CreatedAt = post.CreatedAt,
                }
            );

            if (post.Hashtags is not null)
            {
                for (int i = 0; i < post.Hashtags.Count; i++)
                {
                    hashtagRows.Add(
                        new PostTableHashtagRow
                        {
                            PostId = post.Id,
                            Value = post.Hashtags[i],
                            Ordinal = i,
                        }
                    );
                }
            }

            if (post.Medias is not null)
            {
                for (int i = 0; i < post.Medias.Count; i++)
                {
                    Backup.Domain.Posts.PostMedia media = post.Medias[i];

                    mediaRows.Add(
                        new PostTableMediaRow
                        {
                            PostId = post.Id,
                            Ordinal = i,
                            MediaId = media.Id,
                            Url = media.Url,
                            Type = media.Type,
                            VideoDurationMilis = media.VideoInfo?.DurationMilis,
                        }
                    );

                    if (media.VideoInfo?.Variants is null)
                        continue;

                    for (int j = 0; j < media.VideoInfo.Variants.Count; j++)
                    {
                        Backup.Domain.Posts.PostVariant variant = media.VideoInfo.Variants[j];

                        variantRows.Add(
                            new PostTableMediaVariantRow
                            {
                                PostId = post.Id,
                                MediaOrdinal = i,
                                Ordinal = j,
                                ContentType = variant.ContentType,
                                Bitrate = variant.Bitrate,
                                Url = variant.Url,
                            }
                        );
                    }
                }
            }

            foreach ((string userId, Dictionary<string, Backup.Domain.Posts.IndexData> userIndex) in post.Index)
            {
                foreach ((string origin, Backup.Domain.Posts.IndexData indexData) in userIndex)
                {
                    indexRows.Add(
                        new PostTableIndexEntryRow
                        {
                            PostId = post.Id,
                            UserId = userId,
                            Origin = origin,
                            Previous = indexData.Previous,
                            Next = indexData.Next,
                        }
                    );
                }
            }
        }

        return new PostTableProjectionResult
        {
            Posts = postRows,
            Profiles = [.. profileById.Values],
            Hashtags = hashtagRows,
            Medias = mediaRows,
            MediaVariants = variantRows,
            IndexEntries = indexRows,
        };
    }
}
