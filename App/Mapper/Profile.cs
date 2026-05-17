using Backup.App.Models.Posts;
using Backup.App.Models.Posts.Response;

namespace Backup.App.Mapper;

public class Profile()
{
    public static PostProfile GetProfile(Entry entry)
    {
        TweetResults tweetResults =
            entry.Content.ItemContent.TweetResults ?? throw new Exception("tweetResults");

        Result result = tweetResults.Result;
        Models.Posts.Response.Core resultCore = result.Core ?? throw new Exception("core");
        Result? userResults = resultCore.UserResults.Result;

        string id = result.Legacy?.UserIdStr ?? userResults?.RestId ?? throw new Exception("id");
        string? name = result.Legacy?.Name ?? userResults?.Core?.Name;
        string? userName = result.Legacy?.ScreenName ?? userResults?.Core?.ScreenName;
        string? imageUrl = result.Legacy?.ProfileImageUrlHttps ?? userResults?.Avatar?.ImageUrl;

        bool? following =
            result.Legacy?.Following ?? userResults?.RelationshipPerspectives?.Following;

        int? mediaCount = userResults?.Legacy?.MediaCount;

        PostProfile profile = new()
        {
            Id = id,
            UserName = userName,
            Name = name,
            BannerUrl = string.IsNullOrWhiteSpace(userResults?.Legacy?.ProfileBannerUrl)
                ? null
                : userResults.Legacy.ProfileBannerUrl,
            ImageUrl = imageUrl,
            Following = following,
        };

        if (mediaCount is not null)
            profile.Count = new() { Media = mediaCount };

        return profile;
    }
}
