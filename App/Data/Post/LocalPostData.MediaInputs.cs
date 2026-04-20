namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    private static Models.Post.MediaInput ToMediaInput(Models.Post.Post post) =>
        new()
        {
            Id = post.Id,
            Profile = post.Profile.Clone(),
            Medias = post.Medias?.Select(media => media.Clone()).ToList(),
            Deleted = post.Deleted,
        };

    private static Models.Post.MediaInput ToMediaInput(Models.Post.Data data) =>
        new()
        {
            Id = data.Id,
            Profile = data.Profile.Clone(),
            Medias = data.Medias?.Select(media => media.Clone()).ToList(),
            Deleted = data.Deleted,
        };
}
