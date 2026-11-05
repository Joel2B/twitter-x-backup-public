using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Tests;

public sealed class RelationalMediaInputComposerTests
{
    [Fact]
    public void Compose_PreservesHistoricalMediaProfileAndBanner()
    {
        List<PostMedia> historicalMedia =
        [
            new PostMedia
            {
                Id = "old-media",
                Type = "photo",
                Url = "https://pbs.twimg.com/media/old.jpg",
            },
        ];
        string mediaJson = System.Text.Json.JsonSerializer.Serialize(historicalMedia);
        string imageJson = System.Text.Json.JsonSerializer.Serialize(
            "https://pbs.twimg.com/profile_images/1/old_normal.jpg"
        );
        string bannerJson = System.Text.Json.JsonSerializer.Serialize(
            "https://pbs.twimg.com/profile_banners/1/old"
        );

        List<MediaInput> result = RelationalMediaInputComposer.Compose(
            [],
            [],
            [
                new RelationalHistoricalMediaField(
                    1,
                    "post-1",
                    "profile-1",
                    ChangeFields.PostMedias,
                    mediaJson
                ),
                new RelationalHistoricalMediaField(
                    2,
                    "post-1",
                    "profile-1",
                    ChangeFields.ProfileImageUrl,
                    imageJson
                ),
                new RelationalHistoricalMediaField(
                    3,
                    "post-1",
                    "profile-1",
                    ChangeFields.ProfileBannerUrl,
                    bannerJson
                ),
            ]
        );

        Assert.Contains(
            result.SelectMany(input => input.Medias ?? []),
            media => media.Id == "old-media"
        );
        Assert.Contains(result, input => input.Profile.ImageUrl is not null);
        Assert.Contains(result, input => input.Profile.BannerUrl is not null);
    }
}
