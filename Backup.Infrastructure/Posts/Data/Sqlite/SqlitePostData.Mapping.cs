using Backup.Application.Posts;
using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    private static PostProfileEntity ToProfileEntity(PostProfile profile) =>
        new()
        {
            Id = profile.Id,
            UserName = profile.UserName,
            Name = profile.Name,
            BannerUrl = profile.BannerUrl,
            ImageUrl = profile.ImageUrl,
            Following = profile.Following,
            CountMedia = profile.Count?.Media,
        };

    private static PostEntity ToEntity(
        Post post,
        Dictionary<string, PostProfileEntity> profileById,
        IPostChangeComputationService postChangeComputationService
    )
    {
        PostEntity entity = new()
        {
            Id = post.Id,
            ProfileId = post.Profile.Id,
            Profile = profileById[post.Profile.Id],
            Description = post.Description,
            Retweeted = post.Retweeted,
            Favorited = post.Favorited,
            Bookmarked = post.Bookmarked,
            CreatedAt = post.CreatedAt,
        };

        if (post.Hashtags is not null)
        {
            for (int i = 0; i < post.Hashtags.Count; i++)
                entity.Hashtags.Add(new() { Value = post.Hashtags[i], Ordinal = i });
        }

        if (post.Medias is not null)
        {
            for (int i = 0; i < post.Medias.Count; i++)
            {
                PostMedia media = post.Medias[i];

                PostMediaEntity mediaEntity = new()
                {
                    MediaId = media.Id,
                    Url = media.Url,
                    Type = media.Type,
                    VideoDurationMilis = media.VideoInfo?.DurationMilis,
                    Ordinal = i,
                };

                if (media.VideoInfo?.Variants is not null)
                {
                    for (int j = 0; j < media.VideoInfo.Variants.Count; j++)
                    {
                        PostVariant variant = media.VideoInfo.Variants[j];

                        mediaEntity.Variants.Add(
                            new()
                            {
                                ContentType = variant.ContentType,
                                Bitrate = variant.Bitrate,
                                Url = variant.Url,
                                Ordinal = j,
                            }
                        );
                    }
                }

                entity.Medias.Add(mediaEntity);
            }
        }

        foreach (var userIndex in post.Index)
        {
            foreach (var originIndex in userIndex.Value)
            {
                entity.IndexEntries.Add(
                    new()
                    {
                        UserId = userIndex.Key,
                        Origin = originIndex.Key,
                        Previous = originIndex.Value.Previous,
                        Next = originIndex.Value.Next,
                    }
                );
            }
        }

        Backup.Domain.Posts.Post domainPost = PostReplicationMapper.ToDomain(post);
        IReadOnlyList<PostComputedChange> computedChanges = postChangeComputationService.Compute(
            domainPost
        );

        for (int i = 0; i < computedChanges.Count; i++)
        {
            PostComputedChange change = computedChanges[i];

            PostChangeEntity changeEntity = new()
            {
                PostId = post.Id,
                UserId = change.UserId,
                Date = change.Date,
                ChangeType = change.ChangeType,
            };

            foreach (PostComputedChangeField field in change.Fields)
            {
                changeEntity.Fields.Add(
                    new PostChangeFieldEntity
                    {
                        Field = field.Field,
                        OldValueJson = field.OldValueJson,
                        NewValueJson = field.NewValueJson,
                    }
                );
            }

            entity.Changes.Add(changeEntity);
        }

        return entity;
    }

    private static Post ToModel(PostEntity entity, bool deleted = false)
    {
        List<string>? hashtags = entity
            .Hashtags.OrderBy(o => o.Ordinal)
            .Select(o => o.Value)
            .ToList();

        if (hashtags.Count == 0)
            hashtags = null;

        List<PostMedia>? medias = entity.Medias.OrderBy(o => o.Ordinal).Select(ToModel).ToList();

        if (medias.Count == 0)
            medias = null;

        Dictionary<string, Dictionary<string, IndexData>> index = entity
            .IndexEntries.GroupBy(o => o.UserId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g =>
                    g.ToDictionary(
                        o => o.Origin,
                        o => new IndexData { Previous = o.Previous, Next = o.Next },
                        StringComparer.Ordinal
                    ),
                StringComparer.Ordinal
            );

        Post post = new()
        {
            Id = entity.Id,
            Profile = new()
            {
                Id = entity.Profile.Id,
                UserName = entity.Profile.UserName,
                Name = entity.Profile.Name,
                BannerUrl = entity.Profile.BannerUrl,
                ImageUrl = entity.Profile.ImageUrl,
                Following = entity.Profile.Following,
                Count = entity.Profile.CountMedia.HasValue
                    ? new PostCount { Media = entity.Profile.CountMedia }
                    : null,
            },
            Description = entity.Description,
            Retweeted = entity.Retweeted,
            Favorited = entity.Favorited,
            Bookmarked = entity.Bookmarked,
            CreatedAt = entity.CreatedAt,
            Hashtags = hashtags,
            Medias = medias,
            Deleted = deleted,
            Index = index,
        };

        post.Changes = ToModelChanges(entity.Changes, post);
        return post;
    }

    private static PostMedia ToModel(PostMediaEntity entity)
    {
        List<PostVariant>? variants = entity
            .Variants.OrderBy(o => o.Ordinal)
            .Select(o => new PostVariant
            {
                ContentType = o.ContentType,
                Bitrate = o.Bitrate,
                Url = o.Url,
            })
            .ToList();

        return new PostMedia
        {
            Id = entity.MediaId,
            Url = entity.Url,
            Type = entity.Type,
            VideoInfo =
                entity.VideoDurationMilis is null && (variants is null || variants.Count == 0)
                    ? null
                    : new PostVideoInfo
                    {
                        DurationMilis = entity.VideoDurationMilis,
                        Variants = variants,
                    },
        };
    }
}
