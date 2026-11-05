using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data.Sqlite;

public partial class SqlitePostData
{
    public async Task<List<MediaInput>?> GetMediaInputs()
    {
        PostsDbContext db = await GetDbContext();
        List<PostProfile> profiles = await db
            .Profiles.AsNoTracking()
            .Where(profile => profile.ImageUrl != null || profile.BannerUrl != null)
            .Select(profile => new PostProfile
            {
                Id = profile.Id,
                UserName = profile.UserName,
                Name = profile.Name,
                BannerUrl = profile.BannerUrl,
                ImageUrl = profile.ImageUrl,
                Following = profile.Following,
                Count = profile.CountMedia.HasValue
                    ? new PostCount { Media = profile.CountMedia }
                    : null,
            })
            .ToListAsync();
        List<PostMediaEntity> media = await db
            .PostMedias.AsNoTracking()
            .Include(item => item.Variants)
            .OrderBy(item => item.PostId)
            .ThenBy(item => item.Ordinal)
            .ToListAsync();
        List<RelationalHistoricalMediaField> history = await LoadHistoricalMediaFields(db);
        List<MediaInput> result = RelationalMediaInputComposer.Compose(
            profiles,
            media.Select(item => (item.PostId, ToModel(item))),
            history
        );
        string mediaTypes = string.Join(
            ", ",
            media
                .GroupBy(item => item.Type, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"{group.Key}={group.Count()}")
        );

        _logger.LogInformation(
            "sqlite media inputs projected: profiles={ProfileCount}, postsWithMedia={PostsWithMedia}, medias={MediaCount}, variants={VariantCount}, mediaTypes={MediaTypes}, historicalInputs={HistoricalInputCount}, totalInputs={TotalInputCount}",
            profiles.Count,
            media.Select(item => item.PostId).Distinct(StringComparer.Ordinal).Count(),
            media.Count,
            media.Sum(item => item.Variants.Count),
            mediaTypes,
            history.Count,
            result.Count
        );

        return result.Count == 0 ? null : result;
    }

    private static async Task<List<RelationalHistoricalMediaField>> LoadHistoricalMediaFields(
        PostsDbContext db
    )
    {
        string[] relevantFields =
        [
            ChangeFields.PostMedias,
            ChangeFields.ProfileImageUrl,
            ChangeFields.ProfileBannerUrl,
        ];

        return await db
            .PostChangeFields.AsNoTracking()
            .Where(field => relevantFields.Contains(field.Field) && field.OldValueJson != null)
            .OrderBy(field => field.Id)
            .Select(field => new RelationalHistoricalMediaField(
                field.Id,
                field.Change.PostId,
                field.Change.Post.ProfileId,
                field.Field,
                field.OldValueJson!
            ))
            .ToListAsync();
    }
}
