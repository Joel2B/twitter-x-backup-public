using Backup.Application.Media;
using Backup.Application.Media.Filter;
using Backup.Application.Media.Models;
using Backup.Domain.Posts;

namespace Backup.Tests;

public sealed class MediaDownloadProjectionServiceTests
{
    private readonly MediaDownloadProjectionService _sut =
        new(
            new MediaDownloadFilterPolicyService(),
            new MediaDownloadDataBuilderService(),
            new MediaVideoVariantPolicyService(),
            new MediaDuplicateFilterService()
        );

    [Fact]
    public void Project_BuildsPhotoDownloads_AndAppliesFilters()
    {
        MediaInput post = CreatePost(
            "p1",
            medias:
            [
                new PostMedia
                {
                    Id = "m1",
                    Type = "photo",
                    Url = "https://pbs.twimg.com/media/photo-1.jpg",
                },
            ]
        );

        MediaProcessingResult result = _sut.Project(
            [post],
            CreateConfig(
                photo: new MediaDownloadProjectionRuleConfig
                {
                    Types = ["jpg"],
                    Dimensions = ["orig"],
                    Sizes = ["small"],
                    Filters = ["jpg:jpg:small"],
                }
            )
        );

        MediaDownload all = Assert.Single(result.All);
        MediaDownload filtered = Assert.Single(result.Filtered);

        Assert.Equal("p1", all.Id);
        Assert.Equal(2, all.Data.Count);
        Assert.Single(filtered.Data);
        Assert.Contains(all.Data, item => item.Url.Contains("format=jpg", StringComparison.Ordinal));
        Assert.Contains(
            all.Data,
            item => item.Path.EndsWith(
                Path.Combine("jpg", "dimension", "orig.jpg"),
                StringComparison.Ordinal
            )
        );
        Assert.Contains(
            all.Data,
            item => item.Path.EndsWith(
                Path.Combine("jpg", "size", "small.jpg"),
                StringComparison.Ordinal
            )
        );
        Assert.DoesNotContain(
            filtered.Data,
            item => item.Path.EndsWith(
                Path.Combine("jpg", "size", "small.jpg"),
                StringComparison.Ordinal
            )
        );
    }

    [Fact]
    public void Project_BuildsGifThumbnail_AndMp4Variant()
    {
        MediaInput post = CreatePost(
            "p1",
            medias:
            [
                new PostMedia
                {
                    Id = "gif-1",
                    Type = "animated_gif",
                    Url = "https://pbs.twimg.com/tweet_video_thumb/thumb-gif.jpg",
                    VideoInfo = new PostVideoInfo
                    {
                        Variants =
                        [
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Url = "https://video.twimg.com/tweet_video/gif-variant.mp4?tag=12",
                            },
                        ],
                    },
                },
            ]
        );

        MediaProcessingResult result = _sut.Project(
            [post],
            CreateConfig(
                gif: new MediaDownloadProjectionVariantConfig
                {
                    Thumb = new MediaDownloadProjectionRuleConfig
                    {
                        Types = ["jpg"],
                        Dimensions = ["orig"],
                        Sizes = [],
                    },
                    Types = ["video/mp4"],
                }
            )
        );

        MediaDownload all = Assert.Single(result.All);
        Assert.Equal(2, all.Data.Count);
        Assert.Equal(2, Assert.Single(result.Filtered).Data.Count);
        Assert.Contains(
            all.Data,
            item =>
                item.Url.Contains("format=jpg", StringComparison.Ordinal)
                && item.Path.Contains(Path.Combine("gif", "gif-1", "thumb"), StringComparison.Ordinal)
        );
        Assert.Contains(
            all.Data,
            item =>
                item.Url == "https://video.twimg.com/tweet_video/gif-variant.mp4"
                && item.Path.EndsWith(
                    Path.Combine("mp4", "gif-variant", "index.mp4"),
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Project_BuildsVideoThumbnail_AndVariants()
    {
        MediaInput post = CreatePost(
            "p1",
            medias:
            [
                new PostMedia
                {
                    Id = "video-1",
                    Type = "video",
                    Url = "https://pbs.twimg.com/ext_tw_video_thumb/video-thumb.jpg",
                    VideoInfo = new PostVideoInfo
                    {
                        Variants =
                        [
                            new PostVariant
                            {
                                ContentType = "application/x-mpegURL",
                                Url = "https://video.twimg.com/ext_tw_video/master.m3u8?tag=14",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Url = "https://video.twimg.com/ext_tw_video/1280x720/video.mp4?tag=14",
                            },
                        ],
                    },
                },
            ]
        );

        MediaProcessingResult result = _sut.Project(
            [post],
            CreateConfig(
                video: new MediaDownloadProjectionVariantConfig
                {
                    Thumb = new MediaDownloadProjectionRuleConfig
                    {
                        Types = ["jpg"],
                        Dimensions = ["orig"],
                        Sizes = [],
                    },
                    Types = ["application/x-mpegURL", "video/mp4"],
                }
            )
        );

        MediaDownload all = Assert.Single(result.All);
        Assert.Equal(3, all.Data.Count);
        Assert.Contains(
            all.Data,
            item =>
                item.Url.Contains("format=jpg", StringComparison.Ordinal)
                && item.Path.Contains(
                    Path.Combine("video", "video-1", "thumb"),
                    StringComparison.Ordinal
                )
        );
        Assert.Contains(
            all.Data,
            item =>
                item.Url == "https://video.twimg.com/ext_tw_video/master.m3u8"
                && item.Path.EndsWith(
                    Path.Combine("m3u8", "master", "master.m3u8"),
                    StringComparison.Ordinal
                )
        );
        Assert.Contains(
            all.Data,
            item =>
                item.Url == "https://video.twimg.com/ext_tw_video/1280x720/video.mp4"
                && item.Path.EndsWith(
                    Path.Combine("mp4", "video", "1280x720.mp4"),
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Project_BuildsProfileImage_AndBanner()
    {
        MediaInput post = CreatePost(
            "p1",
            profile: new PostProfile
            {
                Id = "profile-1",
                ImageUrl = "https://pbs.twimg.com/profile_images/avatar_normal.jpg",
                BannerUrl = "https://pbs.twimg.com/profile_banners/banner-1",
            }
        );

        MediaProcessingResult result = _sut.Project(
            [post],
            CreateConfig(
                profile: new MediaDownloadProjectionRuleConfig
                {
                    Dimensions = ["200x200"],
                    Sizes = [],
                },
                banner: new MediaDownloadProjectionRuleConfig
                {
                    Dimensions = ["1500x500"],
                    Sizes = [],
                }
            )
        );

        MediaDownload all = Assert.Single(result.All);
        Assert.Equal(2, all.Data.Count);
        Assert.Contains(
            all.Data,
            item =>
                item.Url == "https://pbs.twimg.com/profile_images/avatar_200x200.jpg"
                && item.Path.Contains(
                    Path.Combine("profiles", "profile-1", "profile"),
                    StringComparison.Ordinal
                )
        );
        Assert.Contains(
            all.Data,
            item =>
                item.Url == "https://pbs.twimg.com/profile_banners/banner-1/1500x500"
                && item.Path.Contains(
                    Path.Combine("profiles", "profile-1", "banner"),
                    StringComparison.Ordinal
                )
        );
    }

    [Fact]
    public void Project_RemovesDuplicateUrls_AndPreservesFirstDownload()
    {
        const string sharedUrl = "https://pbs.twimg.com/media/shared-photo.jpg";

        MediaInput first = CreatePost(
            "p1",
            medias:
            [
                new PostMedia
                {
                    Id = "m1",
                    Type = "photo",
                    Url = sharedUrl,
                },
            ]
        );
        MediaInput second = CreatePost(
            "p2",
            medias:
            [
                new PostMedia
                {
                    Id = "m2",
                    Type = "photo",
                    Url = sharedUrl,
                },
            ]
        );

        MediaProcessingResult result = _sut.Project(
            [first, second],
            CreateConfig(
                photo: new MediaDownloadProjectionRuleConfig
                {
                    Types = ["jpg"],
                    Dimensions = ["orig"],
                    Sizes = [],
                }
            )
        );

        MediaDownload all = Assert.Single(result.All);
        MediaDownload filtered = Assert.Single(result.Filtered);

        Assert.Equal("p1", all.Id);
        Assert.Equal("p1", filtered.Id);
        Assert.Single(all.Data);
        Assert.Single(filtered.Data);
    }

    private static MediaInput CreatePost(
        string id,
        PostProfile? profile = null,
        List<PostMedia>? medias = null
    ) =>
        new()
        {
            Id = id,
            Profile =
                profile
                ?? new PostProfile
                {
                    Id = $"profile-{id}",
                },
            Medias = medias,
        };

    private static MediaDownloadProjectionConfig CreateConfig(
        MediaDownloadProjectionRuleConfig? banner = null,
        MediaDownloadProjectionRuleConfig? profile = null,
        MediaDownloadProjectionRuleConfig? photo = null,
        MediaDownloadProjectionVariantConfig? video = null,
        MediaDownloadProjectionVariantConfig? gif = null
    ) =>
        new()
        {
            Banner = banner ?? CreateEmptyRuleConfig(),
            Profile = profile ?? CreateEmptyRuleConfig(),
            Photo = photo ?? CreateEmptyRuleConfig(),
            Video = video ?? CreateEmptyVariantConfig(),
            Gif = gif ?? CreateEmptyVariantConfig(),
        };

    private static MediaDownloadProjectionRuleConfig CreateEmptyRuleConfig() =>
        new()
        {
            Types = [],
            Dimensions = [],
            Sizes = [],
            Filters = [],
        };

    private static MediaDownloadProjectionVariantConfig CreateEmptyVariantConfig() =>
        new()
        {
            Thumb = CreateEmptyRuleConfig(),
            Types = [],
        };
}
