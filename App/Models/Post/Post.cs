namespace Backup.App.Models.Post;

public class Post : Data
{
    public List<Change> Changes { get; set; } = [];

    public Dictionary<string, Dictionary<string, IndexData>> Index { get; set; } = [];

    public override bool Equals(object? obj)
    {
        if (obj is not Post post)
            return false;

        bool dataEquals =
            Description == post.Description
            && Retweeted == post.Retweeted
            && Favorited == post.Favorited
            && Bookmarked == post.Bookmarked;

        bool profileEquals =
            Profile.UserName == post.Profile.UserName
            && Profile.Name == post.Profile.Name
            && Profile.BannerUrl == post.Profile.BannerUrl
            && Profile.ImageUrl == post.Profile.ImageUrl
            && Profile.Following == post.Profile.Following;

        bool hastagEquals =
            (Hashtags is null && post.Hashtags is null)
            || (
                Hashtags is not null
                && post.Hashtags is not null
                && new HashSet<string>(Hashtags).SetEquals(post.Hashtags)
            );

        bool mediaEquals =
            (Medias is null && post.Medias is null)
            || (
                Medias is not null
                && post.Medias is not null
                && Medias.All(m => post.Medias.Any(lm => lm.Url == m.Url))
                && Medias.Count == post.Medias.Count
            );

        return dataEquals && profileEquals && hastagEquals && mediaEquals;
    }

    public new Post Clone() =>
        new()
        {
            Id = Id,
            Profile = Profile.Clone(),
            Description = Description,
            Retweeted = Retweeted,
            Favorited = Favorited,
            Bookmarked = Bookmarked,
            CreatedAt = CreatedAt,
            Hashtags = Hashtags is null ? null : [.. Hashtags],
            Medias = Medias?.Select(media => media.Clone()).ToList(),
            Deleted = Deleted,
            Changes = CloneChanges(),
            Index = CloneIndex(),
        };

    public List<Change> CloneChanges() => [.. Changes.Select(change => change.Clone())];

    public Dictionary<string, Dictionary<string, IndexData>> CloneIndex() =>
        Index.ToDictionary(o => o.Key, o => o.Value.ToDictionary(o => o.Key, o => o.Value.Clone()));

    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }
}
