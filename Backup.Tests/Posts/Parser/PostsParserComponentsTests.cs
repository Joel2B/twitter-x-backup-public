using Backup.Application.Posts;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;

namespace Backup.Tests;

public class PostsParserComponentsTests
{
    [Theory]
    [InlineData("\"legacy\": { \"media_count\": 14 }")]
    [InlineData("\"tweet_counts\": { \"media_tweets\": 14 }")]
    public void ParseUser_ReadsMediaCount_FromSupportedPayloads(string mediaCountProperty)
    {
        PostParser parser = CreateParser();
        string response = $$"""
            {
              "data": {
                "user": {
                  "result": {
                    "__typename": "User",
                    "rest_id": "1974386208126939136",
                    {{mediaCountProperty}}
                  }
                }
              }
            }
            """;

        Domain.Posts.ParseUser result = parser.ParseUser(response);

        Assert.Equal("1974386208126939136", result.User?.Id);
        Assert.Equal(14, result.User?.MediaCount);
    }

    [Fact]
    public void ParseUser_RejectsPayloadWithoutMediaCount()
    {
        PostParser parser = CreateParser();

        FormatException error = Assert.Throws<FormatException>(
            () =>
                parser.ParseUser(
                    """
                    {
                      "data": {
                        "user": {
                          "result": {
                            "__typename": "User",
                            "rest_id": "1974386208126939136"
                          }
                        }
                      }
                    }
                    """
                )
        );

        Assert.Equal("User parse payload is invalid", error.Message);
    }

    [Fact]
    public void ParseUser_ReturnsNull_ForUnavailableUser()
    {
        PostParser parser = CreateParser();

        Domain.Posts.ParseUser result = parser.ParseUser(
            """
            {
              "data": {
                "user": {
                  "result": {
                    "__typename": "UserUnavailable",
                    "message": "User not found"
                  }
                }
              }
            }
            """
        );

        Assert.Null(result.User);
    }

    [Fact]
    public void TweetResultResolver_UsesTweetWrapper_WhenPresent()
    {
        Result tweet = new() { Legacy = CreateLegacy(id: "tweet-id", userId: "u2") };
        Entry entry = CreateEntry(
            new Result { Legacy = CreateLegacy(id: "base-id", userId: "u1"), Tweet = tweet }
        );

        Result resolved = PostResultResolutionPolicy.ResolvePrimaryThenRetweeted(
            entry.Content.ItemContent.TweetResults!.Result,
            current => current.Tweet,
            current => current.Legacy?.RetweetedStatusResult?.Result
        );

        Assert.Same(tweet, resolved);
        Assert.Equal("tweet-id", resolved.Legacy?.IdStr);
    }

    [Fact]
    public void TweetResultResolver_UsesRetweetedStatus_WhenPresent()
    {
        Result retweetedTweet = new() { Legacy = CreateLegacy(id: "retweet-id", userId: "u3") };
        Result retweetedContainer = new()
        {
            Legacy = CreateLegacy(id: "retweet-container", userId: "u2"),
            Tweet = retweetedTweet,
        };

        Legacy legacy = CreateLegacy(id: "base-id", userId: "u1");
        legacy.RetweetedStatusResult = new TweetResults { Result = retweetedContainer };

        Entry entry = CreateEntry(new Result { Legacy = legacy });

        Result resolved = PostResultResolutionPolicy.ResolvePrimaryThenRetweeted(
            entry.Content.ItemContent.TweetResults!.Result,
            current => current.Tweet,
            current => current.Legacy?.RetweetedStatusResult?.Result
        );

        Assert.Same(retweetedTweet, resolved);
        Assert.Equal("retweet-id", resolved.Legacy?.IdStr);
    }

    [Fact]
    public void TimelineEntryExtractor_ExtractsEntriesAndBottomCursor_FromTimelineItems()
    {
        IPostTimelineExtractionService service = new PostTimelineExtractor();
        JObject root = JObject.Parse(
            """
            {
              "data": {
                "entries": [
                  {
                    "entryId": "tweet-1",
                    "content": {
                      "entryType": "TimelineTimelineItem",
                      "itemContent": {
                        "tweet_results": {
                          "result": {
                            "core": {
                              "name": "n",
                              "screen_name": "s",
                              "user_results": { "result": null }
                            }
                          }
                        }
                      }
                    }
                  },
                  {
                    "entryId": "cursor-bottom",
                    "content": {
                      "entryType": "TimelineTimelineCursor",
                      "cursorType": "Bottom",
                      "value": "CURSOR_ABC",
                      "itemContent": {}
                    }
                  }
                ]
              }
            }
            """
        );

        List<Entry> entries = service
            .ExtractEntries(root)
            .Select(entry => entry.ToObject<Entry>() ?? throw new Exception())
            .ToList();
        string? cursor = service.ExtractCursor(root);

        Assert.Single(entries);
        Assert.Equal("tweet-1", entries[0].EntryId);
        Assert.Equal("CURSOR_ABC", cursor);
    }

    [Fact]
    public void TimelineEntryExtractor_FallsBackToModuleItems()
    {
        IPostTimelineExtractionService service = new PostTimelineExtractor();
        JObject root = JObject.Parse(
            """
            {
              "data": {
                "entries": [
                  {
                    "entryId": "non-tweet",
                    "content": {
                      "entryType": "TimelineTimelineItem",
                      "itemContent": {}
                    }
                  }
                ],
                "moduleItems": [
                  {
                    "entryId": "module-tweet-1",
                    "item": {
                      "itemContent": {
                        "tweet_results": {
                          "result": {
                            "core": {
                              "name": "n",
                              "screen_name": "s",
                              "user_results": { "result": null }
                            }
                          }
                        }
                      }
                    },
                    "content": {
                      "entryType": "placeholder",
                      "itemContent": {}
                    }
                  }
                ]
              }
            }
            """
        );

        List<Entry> entries = service
            .ExtractEntries(root)
            .Select(entry => entry.ToObject<Entry>() ?? throw new Exception())
            .ToList();

        Assert.Single(entries);
        Assert.Equal("module-tweet-1", entries[0].EntryId);
        Assert.Equal("", entries[0].Content.EntryType);
        Assert.NotNull(entries[0].Content.ItemContent.TweetResults);
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

    private static Legacy CreateLegacy(string id, string userId) =>
        new()
        {
            Entities = new Entities { Hashtags = [] },
            Name = "name",
            ProfileImageUrlHttps = "https://img.local/profile.jpg",
            ScreenName = "screen",
            Favorited = false,
            Retweeted = false,
            UserIdStr = userId,
            IdStr = id,
            FullText = "text",
            CreatedAt = "Sun May 24 04:00:00 +0000 2026",
        };

    private static PostParser CreateParser() =>
        new(
            NullLogger<PostParser>.Instance,
            new PostTimelineExtractor(),
            new PostUserParsePolicyService(),
            new PostProjectionParseService(),
            new PostTokenMaterializer()
        );
}
