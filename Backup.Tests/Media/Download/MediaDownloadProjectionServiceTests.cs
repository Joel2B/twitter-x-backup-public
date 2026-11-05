using Backup.Application.Media;
using Backup.Application.Media.Filter;
using Backup.Application.Media.Models;
using Backup.Domain.Posts;

namespace Backup.Tests;

public sealed class MediaDownloadProjectionServiceTests
{
    private readonly MediaDownloadProjectionService _sut = new(
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
        Assert.Contains(
            all.Data,
            item => item.Url.Contains("format=jpg", StringComparison.Ordinal)
        );
        Assert.Contains(
            all.Data,
            item =>
                item.Path.EndsWith(
                    Path.Combine("jpg", "dimension", "orig.jpg"),
                    StringComparison.Ordinal
                )
        );
        Assert.Contains(
            all.Data,
            item =>
                item.Path.EndsWith(
                    Path.Combine("jpg", "size", "small.jpg"),
                    StringComparison.Ordinal
                )
        );
        Assert.DoesNotContain(
            filtered.Data,
            item =>
                item.Path.EndsWith(
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
                && item.Path.Contains(
                    Path.Combine("gif", "gif-1", "thumb"),
                    StringComparison.Ordinal
                )
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
                                Url =
                                    "https://video.twimg.com/ext_tw_video/1280x720/video.mp4?tag=14",
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
    public void Project_BuildsRealBookmarkVideo()
    {
        MediaInput post = CreatePost(
            "2117639865374515612",
            medias:
            [
                new PostMedia
                {
                    Id = "2117639805680947200",
                    Type = "video",
                    Url =
                        "https://pbs.twimg.com/amplify_video_thumb/2117639805680947200/img/377yWa_SdlfZWHAt.jpg",
                    VideoInfo = new PostVideoInfo
                    {
                        Variants =
                        [
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 632000,
                                Url =
                                    "https://video.twimg.com/amplify_video/2117639805680947200/vid/avc1/320x466/30YMZVV06Dcq-R0w.mp4?tag=14",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 950000,
                                Url =
                                    "https://video.twimg.com/amplify_video/2117639805680947200/vid/avc1/480x700/95pqgElkwsmIrzOn.mp4?tag=14",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 2176000,
                                Url =
                                    "https://video.twimg.com/amplify_video/2117639805680947200/vid/avc1/720x1050/Z9Xk_IECdg4wWlND.mp4?tag=14",
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
                        Types = ["jpg", "png"],
                        Dimensions = [],
                        Sizes = ["orig", "large"],
                        Filters = ["jpg:png:*", "jpg:jpg:large", "png:jpg:orig", "png:png:large"],
                    },
                    Types = ["video/mp4"],
                    VariantSelection = "highest-resolution",
                }
            )
        );

        MediaDownload filtered = Assert.Single(result.Filtered);
        Assert.Contains(filtered.Data, item => item.Path.EndsWith("720x1050.mp4"));
        Assert.Contains(
            filtered.Data,
            item =>
                item.Path.Contains(Path.Combine("video", "2117639805680947200", "thumb"))
                && item.Path.EndsWith(Path.Combine("size", "orig.jpg"))
        );
    }

    [Fact]
    public void Project_BuildsRealLikedPhoto()
    {
        MediaInput post = CreatePost(
            "2118129052951392614",
            medias:
            [
                new PostMedia
                {
                    Id = "2118129040980848640",
                    Type = "photo",
                    Url = "https://pbs.twimg.com/media/HWUbI_QaMAAsU7h.jpg",
                },
            ]
        );

        MediaProcessingResult result = _sut.Project(
            [post],
            CreateConfig(
                photo: new MediaDownloadProjectionRuleConfig
                {
                    Types = ["jpg", "png"],
                    Dimensions = [],
                    Sizes = ["orig", "large"],
                    Filters = ["jpg:png:*", "jpg:jpg:large", "png:jpg:orig", "png:png:large"],
                }
            )
        );

        MediaDownload filtered = Assert.Single(result.Filtered);
        MediaDownloadData photo = Assert.Single(filtered.Data);
        Assert.Equal(
            Path.Combine(
                "2118129052951392614",
                "photo",
                "2118129040980848640",
                "HWUbI_QaMAAsU7h",
                "jpg",
                "size",
                "orig.jpg"
            ),
            photo.Path
        );
    }

    [Fact]
    public void Project_SelectsHighestResolutionMp4_AndKeepsHls()
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
                                ContentType = "video/mp4",
                                Bitrate = 256000,
                                Url = "https://video.twimg.com/ext_tw_video/320x180/video.mp4",
                            },
                            new PostVariant
                            {
                                ContentType = "application/x-mpegURL",
                                Url = "https://video.twimg.com/ext_tw_video/master.m3u8",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 2176000,
                                Url = "https://video.twimg.com/ext_tw_video/1280x720/video.mp4",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 832000,
                                Url = "https://video.twimg.com/ext_tw_video/640x360/video.mp4",
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
                    Thumb = CreateEmptyRuleConfig(),
                    Types = ["application/x-mpegURL", "video/mp4"],
                    VariantSelection = "highest-resolution",
                }
            )
        );

        MediaDownload all = Assert.Single(result.All);
        Assert.Equal(2, all.Data.Count);
        Assert.Contains(all.Data, item => item.Path.EndsWith("1280x720.mp4"));
        Assert.Contains(all.Data, item => item.Path.EndsWith("master.m3u8"));
        Assert.DoesNotContain(all.Data, item => item.Path.EndsWith("320x180.mp4"));
        Assert.DoesNotContain(all.Data, item => item.Path.EndsWith("640x360.mp4"));
    }

    [Fact]
    public void Project_HighestResolutionUsesBitrateToBreakTies()
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
                                ContentType = "video/mp4",
                                Bitrate = 832000,
                                Url = "https://video.twimg.com/a/1280x720/low.mp4",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 2176000,
                                Url = "https://video.twimg.com/b/1280x720/high.mp4",
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
                    Thumb = CreateEmptyRuleConfig(),
                    Types = ["video/mp4"],
                    VariantSelection = "highest-resolution",
                }
            )
        );

        MediaDownloadData selected = Assert.Single(Assert.Single(result.All).Data);
        Assert.Equal("https://video.twimg.com/b/1280x720/high.mp4", selected.Url);
    }

    [Fact]
    public void Project_HighestResolutionFallsBackToBitrateWhenResolutionIsUnknown()
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
                                ContentType = "video/mp4",
                                Bitrate = 832000,
                                Url = "https://video.twimg.com/a/low.mp4",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 2176000,
                                Url = "https://video.twimg.com/b/high.mp4",
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
                    Thumb = CreateEmptyRuleConfig(),
                    Types = ["video/mp4"],
                    VariantSelection = "highest-resolution",
                }
            )
        );

        MediaDownloadData selected = Assert.Single(Assert.Single(result.All).Data);
        Assert.Equal("https://video.twimg.com/b/high.mp4", selected.Url);
    }

    [Fact]
    public void Project_HighestResolutionPrefersKnownResolutionOverUnknownVariant()
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
                                ContentType = "video/mp4",
                                Bitrate = 2176000,
                                Url = "https://video.twimg.com/a/unknown.mp4",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Bitrate = 832000,
                                Url = "https://video.twimg.com/b/640x360/video.mp4",
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
                    Thumb = CreateEmptyRuleConfig(),
                    Types = ["video/mp4"],
                    VariantSelection = "highest-resolution",
                }
            )
        );

        MediaDownloadData selected = Assert.Single(Assert.Single(result.All).Data);
        Assert.Equal("https://video.twimg.com/b/640x360/video.mp4", selected.Url);
    }

    [Fact]
    public void Project_DoesNotApplyVideoVariantSelectionToAnimatedGif()
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
                                Url = "https://video.twimg.com/tweet_video/a.mp4",
                            },
                            new PostVariant
                            {
                                ContentType = "video/mp4",
                                Url = "https://video.twimg.com/tweet_video/b.mp4",
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
                    Thumb = CreateEmptyRuleConfig(),
                    Types = ["video/mp4"],
                    VariantSelection = "highest-resolution",
                }
            )
        );

        Assert.Equal(2, Assert.Single(result.All).Data.Count);
    }

    [Theory]
    [InlineData("video", "video")]
    [InlineData("animated_gif", "gif")]
    public void Project_BuildsThumbnail_WhenVideoInfoIsMissing(string type, string pathType)
    {
        MediaInput post = CreatePost(
            "p1",
            medias:
            [
                new PostMedia
                {
                    Id = "media-1",
                    Type = type,
                    Url = "https://pbs.twimg.com/media/thumb.jpg",
                    VideoInfo = null,
                },
            ]
        );

        MediaDownloadProjectionVariantConfig rule = new()
        {
            Thumb = CreateMediaRule(),
            Types = ["video/mp4"],
        };
        MediaProcessingResult result = _sut.Project(
            [post],
            CreateConfig(
                video: type == "video" ? rule : null,
                gif: type == "animated_gif" ? rule : null
            )
        );

        MediaDownloadData thumbnail = Assert.Single(Assert.Single(result.Filtered).Data);
        Assert.Contains(Path.Combine(pathType, "media-1", "thumb"), thumbnail.Path);
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
            Profile = profile ?? new PostProfile { Id = $"profile-{id}" },
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

    private static MediaDownloadProjectionRuleConfig CreateMediaRule() =>
        new()
        {
            Types = ["jpg"],
            // Microsoft.Extensions.Configuration binds an empty JSON array as null.
            Dimensions = null,
            Sizes = ["orig"],
            Filters = [],
        };

    private static MediaDownloadProjectionVariantConfig CreateEmptyVariantConfig() =>
        new() { Thumb = CreateEmptyRuleConfig(), Types = [] };
}
