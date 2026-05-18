using Backup.App.Models.Posts;

namespace Backup.App.Data.Posts;

public partial class LocalPostData
{
    private static MediaInput ToMediaInput(Post post) =>
        new()
        {
            Id = post.Id,
            Profile = post.Profile.Clone(),
            Medias = post.Medias?.Select(media => media.Clone()).ToList(),
            Deleted = post.Deleted,
        };

    private static MediaInput ToMediaInput(PostData data) =>
        new()
        {
            Id = data.Id,
            Profile = data.Profile.Clone(),
            Medias = data.Medias?.Select(media => media.Clone()).ToList(),
            Deleted = data.Deleted,
        };
}
