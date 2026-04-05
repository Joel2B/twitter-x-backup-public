namespace Backup.App.Models.Post;

internal static class ChangeFields
{
    private const string PostPrefix = "post.";
    private const string ProfilePrefix = "profile.";
    private const string IndexPrefix = "index.";

    public const string PostId = "post.id";
    public const string PostDescription = "post.description";
    public const string PostRetweeted = "post.retweeted";
    public const string PostFavorited = "post.favorited";
    public const string PostBookmarked = "post.bookmarked";
    public const string PostCreatedAt = "post.created_at";
    public const string PostDeleted = "post.deleted";
    public const string PostHashtags = "post.hashtags";
    public const string PostMedias = "post.medias";

    public const string ProfileId = "profile.id";
    public const string ProfileUserName = "profile.user_name";
    public const string ProfileName = "profile.name";
    public const string ProfileBannerUrl = "profile.banner_url";
    public const string ProfileImageUrl = "profile.image_url";
    public const string ProfileFollowing = "profile.following";
    public const string ProfileCountMedia = "profile.count_media";

    public static string Index(string userId) => $"{IndexPrefix}{userId}";

    public static bool IsPostOrProfile(string field) =>
        field.StartsWith(PostPrefix, StringComparison.Ordinal)
        || field.StartsWith(ProfilePrefix, StringComparison.Ordinal);

    public static bool IsIndex(string field) =>
        field.StartsWith(IndexPrefix, StringComparison.Ordinal);

    public static string? GetIndexUserId(string field)
    {
        if (!IsIndex(field))
            return null;

        string userId = field[IndexPrefix.Length..];
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }
}
