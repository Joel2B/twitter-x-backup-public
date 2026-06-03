using Backup.Application.Posts.Models;

namespace Backup.Application.Posts;

public sealed class PostTableMaterializationService : IPostTableMaterializationService
{
    public IReadOnlyList<Backup.Domain.Posts.Post> Materialize(PostTableMaterializationInput input)
    {
        Dictionary<string, Backup.Domain.Posts.PostProfile> profiles = input.Profiles.ToDictionary(
            row => row.Id,
            row => new Backup.Domain.Posts.PostProfile
            {
                Id = row.Id,
                UserName = row.UserName,
                Name = row.Name,
                BannerUrl = row.BannerUrl,
                ImageUrl = row.ImageUrl,
                Following = row.Following,
                Count = row.CountMedia.HasValue
                    ? new Backup.Domain.Posts.PostCount { Media = row.CountMedia }
                    : null,
            },
            StringComparer.Ordinal
        );

        Dictionary<string, List<string>> hashtagsByPost = input
            .Hashtags.GroupBy(row => row.PostId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(row => row.Ordinal).Select(row => row.Value).ToList(),
                StringComparer.Ordinal
            );

        Dictionary<
            (string PostId, int MediaOrdinal),
            List<Backup.Domain.Posts.PostVariant>
        > variantsByMedia = input
            .MediaVariants.GroupBy(row => (row.PostId, row.MediaOrdinal))
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .OrderBy(row => row.Ordinal)
                        .Select(row => new Backup.Domain.Posts.PostVariant
                        {
                            ContentType = row.ContentType,
                            Bitrate = row.Bitrate,
                            Url = row.Url,
                        })
                        .ToList()
            );

        Dictionary<string, List<Backup.Domain.Posts.PostMedia>> mediasByPost = input
            .Medias.GroupBy(row => row.PostId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .OrderBy(row => row.Ordinal)
                        .Select(row =>
                        {
                            variantsByMedia.TryGetValue(
                                (row.PostId, row.Ordinal),
                                out List<Backup.Domain.Posts.PostVariant>? variants
                            );

                            return new Backup.Domain.Posts.PostMedia
                            {
                                Id = row.MediaId,
                                Url = row.Url,
                                Type = row.Type,
                                VideoInfo =
                                    row.VideoDurationMilis is null
                                    && (variants is null || variants.Count == 0)
                                        ? null
                                        : new Backup.Domain.Posts.PostVideoInfo
                                        {
                                            DurationMilis = row.VideoDurationMilis,
                                            Variants = variants,
                                        },
                            };
                        })
                        .ToList(),
                StringComparer.Ordinal
            );

        Dictionary<
            string,
            Dictionary<string, Dictionary<string, Backup.Domain.Posts.IndexData>>
        > indexByPost = input
            .IndexEntries.GroupBy(row => row.PostId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .GroupBy(row => row.UserId, StringComparer.Ordinal)
                        .ToDictionary(
                            userGroup => userGroup.Key,
                            userGroup =>
                                userGroup.ToDictionary(
                                    row => row.Origin,
                                    row => new Backup.Domain.Posts.IndexData
                                    {
                                        Previous = row.Previous,
                                        Next = row.Next,
                                    },
                                    StringComparer.Ordinal
                                ),
                            StringComparer.Ordinal
                        ),
                StringComparer.Ordinal
            );

        Dictionary<string, PostTableMetaRow> metaById = input.PostMeta.ToDictionary(
            row => row.Id,
            row => row,
            StringComparer.Ordinal
        );

        List<Backup.Domain.Posts.Post> posts = new(input.Posts.Count);

        foreach (PostTablePostRow row in input.Posts)
        {
            Backup.Domain.Posts.PostProfile profile = profiles.TryGetValue(
                row.ProfileId,
                out Backup.Domain.Posts.PostProfile? value
            )
                ? value
                : new Backup.Domain.Posts.PostProfile { Id = row.ProfileId };

            hashtagsByPost.TryGetValue(row.Id, out List<string>? hashtags);
            mediasByPost.TryGetValue(row.Id, out List<Backup.Domain.Posts.PostMedia>? medias);
            indexByPost.TryGetValue(
                row.Id,
                out Dictionary<string, Dictionary<string, Backup.Domain.Posts.IndexData>>? index
            );

            if (!metaById.TryGetValue(row.Id, out PostTableMetaRow? meta))
                throw new KeyNotFoundException($"Missing post_meta row for post '{row.Id}'.");

            posts.Add(
                new Backup.Domain.Posts.Post
                {
                    Id = row.Id,
                    Profile = profile,
                    Description = row.Description,
                    Retweeted = row.Retweeted,
                    Favorited = row.Favorited,
                    Bookmarked = row.Bookmarked,
                    CreatedAt = row.CreatedAt,
                    Deleted = meta.Deleted,
                    Hashtags = hashtags is not null && hashtags.Count > 0 ? hashtags : null,
                    Medias = medias is not null && medias.Count > 0 ? medias : null,
                    Index = index ?? [],
                }
            );
        }

        return posts;
    }
}
