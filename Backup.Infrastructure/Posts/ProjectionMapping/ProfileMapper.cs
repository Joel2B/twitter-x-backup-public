using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Posts.Response;

namespace Backup.Infrastructure.Posts.ProjectionMapping;

public static class ProfileMapper
{
    public static ParsedPostProfileProjection Map(Result result)
    {
        CoreUser resultCore = result.Core ?? throw new Exception("core");
        Result? userResults = resultCore.UserResults.Result;

        string id = result.Legacy?.UserIdStr ?? userResults?.RestId ?? throw new Exception("id");
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
