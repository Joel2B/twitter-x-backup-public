using Backup.Application.Posts.Models;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Posts.Adapters.ProjectionMapping;

public static class ProfileMapper
{
    public static ParsedPostProfileProjection Map(Result result)
    {
        CoreUser resultCore =
            result.Core ?? throw new FormatException("Tweet payload is missing core user data.");

        Result? userResults = resultCore.UserResults.Result;

        string id =
            result.Legacy?.UserIdStr
            ?? userResults?.RestId
            ?? throw new FormatException("Tweet payload is missing profile id.");

        string? name = result.Legacy?.Name ?? userResults?.Core?.Name;
        string? userName = result.Legacy?.ScreenName ?? userResults?.Core?.ScreenName;
        string? imageUrl = result.Legacy?.ProfileImageUrlHttps ?? userResults?.Avatar?.ImageUrl;

        bool? following =
            result.Legacy?.Following ?? userResults?.RelationshipPerspectives?.Following;

        int? mediaCount = userResults?.Legacy?.MediaCount;

        return new ParsedPostProfileProjection
        {
            Id = id,
            UserName = userName,
            Name = name,
            BannerUrl = string.IsNullOrWhiteSpace(userResults?.Legacy?.ProfileBannerUrl)
                ? null
                : userResults.Legacy.ProfileBannerUrl,
            ImageUrl = imageUrl,
            Following = following,
            MediaCount = mediaCount,
        };
    }
}
