using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Models;
using Backup.Infrastructure.Posts.Adapters.ProjectionMapping;

namespace Backup.Tests;

public class PostProjectionMappingTests
{
    [Fact]
    public void PostMapper_MapsBasicLegacyFields()
    {
        Entry entry = CreateEntry(
            CreateResult(
                CreateLegacy(
                    id: "p1",
                    userId: "u1",
                    fullText: "hello world",
                    hashtags: [new Hashtag { Indices = [0, 1], Text = "tag1" }]
                )
            )
        );

        ParsedPostProjection post = PostMapper.Map(entry);

        Assert.Equal("p1", post.Id);
        Assert.Equal("u1", post.Profile.Id);
        Assert.Equal("hello world", post.Description);
        Assert.Equal("screen-u1", post.Profile.UserName);
        Assert.NotNull(post.Hashtags);
        Assert.Single(post.Hashtags!);
        Assert.Equal("tag1", post.Hashtags[0]);
        Assert.Null(post.Medias);
    }

    [Fact]
    public void PostMapper_UsesRetweetedStatusResult_WhenPresent()
    {
        Result retweetedResult = CreateResult(
            CreateLegacy(
                id: "retweeted-post",
                userId: "u-retweet",
                fullText: "from retweet source"
            )
        );

        Legacy baseLegacy = CreateLegacy(id: "base-post", userId: "u-base", fullText: "base");
        baseLegacy.RetweetedStatusResult = new TweetResults { Result = retweetedResult };

        Entry entry = CreateEntry(CreateResult(baseLegacy));

        ParsedPostProjection post = PostMapper.Map(entry);

        Assert.Equal("retweeted-post", post.Id);
        Assert.Equal("u-retweet", post.Profile.Id);
        Assert.Equal("from retweet source", post.Description);
    }

    [Fact]
    public void PostMapper_ReturnsNullHashtags_WhenOnlyWhitespaceHashtagsExist()
    {
        Entry entry = CreateEntry(
            CreateResult(
                CreateLegacy(
                    id: "p2",
                    userId: "u2",
                    fullText: "no hashtags",
                    hashtags:
                    [
                        new Hashtag { Indices = [0, 1], Text = "   " },
                        new Hashtag { Indices = [0, 1], Text = "" },
                    ]
                )
            )
        );

        ParsedPostProjection post = PostMapper.Map(entry);

        Assert.Null(post.Hashtags);
    }

    [Fact]
    public void PostMapper_MapsMediaFromUnifiedCard_WhenLegacyMediaIsMissing()
    {
        Legacy legacy = CreateLegacy(id: "p3", userId: "u3", fullText: "with card media");
        legacy.Entities.Media = null;

        Result result = CreateResult(legacy);
        result.Card = new Card
        {
            LegacyCard = new LegacyCard
            {
                BindingValues =
                [
                    new Binding
                    {
                        Key = "unified_card",
                        Value = new BindingValue
                        {
                            Type = "STRING",
                            StringValue =
                                """
                                {
                                  "component_objects": {
                                    "media_1": { "data": { "id": "m1" } }
                                  },
                                  "media_entities": {
                                    "m1": {
                                      "id_str": "m1",
                                      "media_url_https": "https://img.local/m1.jpg",
                                      "type": "photo"
                                    }
                                  }
                                }
                                """,
                        },
                    },
                ],
            },
        };

        Entry entry = CreateEntry(result);

        ParsedPostProjection post = PostMapper.Map(entry);

        Assert.NotNull(post.Medias);
        Assert.Single(post.Medias!);
        Assert.Equal("m1", post.Medias[0].Id);
        Assert.Equal("photo", post.Medias[0].Type);
        Assert.Equal("https://img.local/m1.jpg", post.Medias[0].Url);
    }

    [Fact]
    public void PostMapper_UsesUserResultsFallback_WhenLegacyProfileFieldsAreMissing()
    {
        Legacy legacy = CreateLegacy(id: "p4", userId: "u4", fullText: "fallback profile");
        legacy.Name = null!;
        legacy.ScreenName = null!;
        legacy.ProfileImageUrlHttps = null!;
        legacy.Following = null;

        Result userResult = new()
        {
            RestId = "u4",
            Core = new CoreUser
            {
                Name = "user-results-name",
                ScreenName = "user-results-screen",
                UserResults = new UserResults { Result = null },
            },
            Avatar = new Avatar { ImageUrl = "https://img.local/u4.png" },
            RelationshipPerspectives = new RelationshipPerspectives { Following = true },
            Legacy = new Legacy
            {
                Entities = new Entities { Hashtags = [] },
                Name = "legacy-user-result",
                ProfileImageUrlHttps = "https://img.local/legacy.png",
                ScreenName = "legacy-screen-result",
                Favorited = false,
                Retweeted = false,
                UserIdStr = "u4",
                MediaCount = 999,
            },
        };

        Result result = CreateResult(legacy);
        result.Core = new CoreUser
        {
            Name = "base-name",
            ScreenName = "base-screen",
            UserResults = new UserResults { Result = userResult },
        };

        Entry entry = CreateEntry(result);

        ParsedPostProjection post = PostMapper.Map(entry);

        Assert.Equal("u4", post.Profile.Id);
        Assert.Equal("user-results-name", post.Profile.Name);
        Assert.Equal("user-results-screen", post.Profile.UserName);
        Assert.Equal("https://img.local/u4.png", post.Profile.ImageUrl);
        Assert.True(post.Profile.Following);
        Assert.Equal(999, post.Profile.MediaCount);
    }

    private static Entry CreateEntry(Result result) =>
        new()
        {
            EntryId = "entry-1",
            Content = new Content
            {
                EntryType = "TimelineTimelineItem",
                ItemContent = new ItemContent
                {
                    TweetResults = new TweetResults { Result = result },
                },
            },
        };

    private static Result CreateResult(Legacy legacy) =>
        new()
        {
            Legacy = legacy,
            Core = new CoreUser
            {
                Name = "core-name",
                ScreenName = "core-screen",
                UserResults = new UserResults { Result = null },
            },
        };

    private static Legacy CreateLegacy(
        string id,
        string userId,
        string fullText,
        List<Hashtag>? hashtags = null
    ) =>
        new()
        {
            Entities = new Entities
            {
                Hashtags = hashtags ?? [],
                Media = null,
            },
            Name = $"name-{userId}",
            ProfileImageUrlHttps = $"https://img.local/{userId}.jpg",
            ScreenName = $"screen-{userId}",
            Favorited = false,
            Retweeted = false,
            UserIdStr = userId,
            IdStr = id,
            FullText = fullText,
            CreatedAt = "Sun May 24 04:00:00 +0000 2026",
            Bookmarked = false,
        };
}
