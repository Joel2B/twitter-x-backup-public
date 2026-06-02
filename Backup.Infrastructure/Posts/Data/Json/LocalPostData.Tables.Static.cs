using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;
using Backup.Infrastructure.Utils;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    private static readonly IReadOnlyList<TableManifestEntry> TableManifest =
    [
        new(NormalizedPostsFileName, tables => tables.Posts),
        new(ProfilesFileName, tables => tables.Profiles),
        new(HashtagsFileName, tables => tables.Hashtags),
        new(MediasFileName, tables => tables.Medias),
        new(MediaVariantsFileName, tables => tables.MediaVariants),
        new(IndexEntriesFileName, tables => tables.IndexEntries),
        new(PostChangesFileName, tables => tables.PostChanges),
        new(PostChangeFieldsFileName, tables => tables.PostChangeFields),
    ];

    private LocalPostTables BuildTables(List<Post> posts)
    {
        LocalPostTables tables = new();
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts = posts
            .Select(PostReplicationMapper.ToDomain)
            .ToList();
        PostTableProjectionResult projection = _tableCoordinator.Project(domainPosts);

        tables.Posts = projection
            .Posts.Select(row => new PostRow
            {
                Id = row.Id,
                ProfileId = row.ProfileId,
                Description = row.Description,
                Retweeted = row.Retweeted,
                Favorited = row.Favorited,
                Bookmarked = row.Bookmarked,
                CreatedAt = row.CreatedAt,
            })
            .ToList();
        tables.Profiles = projection
            .Profiles.Select(row => new ProfileRow
            {
                Id = row.Id,
                UserName = row.UserName,
                Name = row.Name,
                BannerUrl = row.BannerUrl,
                ImageUrl = row.ImageUrl,
                Following = row.Following,
                CountMedia = row.CountMedia,
            })
            .ToList();
        tables.Hashtags = projection
            .Hashtags.Select(row => new HashtagRow
            {
                PostId = row.PostId,
                Value = row.Value,
                Ordinal = row.Ordinal,
            })
            .ToList();
        tables.Medias = projection
            .Medias.Select(row => new MediaRow
            {
                PostId = row.PostId,
                Ordinal = row.Ordinal,
                MediaId = row.MediaId,
                Url = row.Url,
                Type = row.Type,
                VideoDurationMilis = row.VideoDurationMilis,
            })
            .ToList();
        tables.MediaVariants = projection
            .MediaVariants.Select(row => new MediaVariantRow
            {
                PostId = row.PostId,
                MediaOrdinal = row.MediaOrdinal,
                Ordinal = row.Ordinal,
                ContentType = row.ContentType,
                Bitrate = row.Bitrate,
                Url = row.Url,
            })
            .ToList();
        tables.IndexEntries = projection
            .IndexEntries.Select(row => new IndexEntryRow
            {
                PostId = row.PostId,
                UserId = row.UserId,
                Origin = row.Origin,
                Previous = row.Previous,
                Next = row.Next,
            })
            .ToList();

        foreach (Post post in posts)
            AddChangeRows(tables, post);

        return tables;
    }

    private List<Post> BuildPosts(LocalPostTables tables)
    {
        Dictionary<string, List<PostChangeRow>> changesByPost = tables
            .PostChanges.GroupBy(o => o.PostId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(o => o.Date).ThenBy(o => o.Ordinal).ToList(),
                StringComparer.Ordinal
            );

        Dictionary<(string PostId, int ChangeOrdinal), List<PostChangeFieldRow>> fieldsByChange =
            tables
                .PostChangeFields.GroupBy(o => (o.PostId, o.ChangeOrdinal))
                .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Ordinal).ToList());
        IReadOnlyList<Backup.Domain.Posts.Post> domainPosts =
            _tableCoordinator.Materialize(
                new PostTableMaterializationInput
                {
                    Posts = tables
                        .Posts.Select(row => new PostTablePostRow
                        {
                            Id = row.Id,
                            ProfileId = row.ProfileId,
                            Description = row.Description,
                            Retweeted = row.Retweeted,
                            Favorited = row.Favorited,
                            Bookmarked = row.Bookmarked,
                            CreatedAt = row.CreatedAt,
                        })
                        .ToList(),
                    Profiles = tables
                        .Profiles.Select(row => new PostTableProfileRow
                        {
                            Id = row.Id,
                            UserName = row.UserName,
                            Name = row.Name,
                            BannerUrl = row.BannerUrl,
                            ImageUrl = row.ImageUrl,
                            Following = row.Following,
                            CountMedia = row.CountMedia,
                        })
                        .ToList(),
                    Hashtags = tables
                        .Hashtags.Select(row => new PostTableHashtagRow
                        {
                            PostId = row.PostId,
                            Value = row.Value,
                            Ordinal = row.Ordinal,
                        })
                        .ToList(),
                    Medias = tables
                        .Medias.Select(row => new PostTableMediaRow
                        {
                            PostId = row.PostId,
                            Ordinal = row.Ordinal,
                            MediaId = row.MediaId,
                            Url = row.Url,
                            Type = row.Type,
                            VideoDurationMilis = row.VideoDurationMilis,
                        })
                        .ToList(),
                    MediaVariants = tables
                        .MediaVariants.Select(row => new PostTableMediaVariantRow
                        {
                            PostId = row.PostId,
                            MediaOrdinal = row.MediaOrdinal,
                            Ordinal = row.Ordinal,
                            ContentType = row.ContentType,
                            Bitrate = row.Bitrate,
                            Url = row.Url,
                        })
                        .ToList(),
                    IndexEntries = tables
                        .IndexEntries.Select(row => new PostTableIndexEntryRow
                        {
                            PostId = row.PostId,
                            UserId = row.UserId,
                            Origin = row.Origin,
                            Previous = row.Previous,
                            Next = row.Next,
                        })
                        .ToList(),
                    PostMeta = tables
                        .PostMeta.Select(row => new PostTableMetaRow
                        {
                            Id = row.Id,
                            Deleted = row.Deleted,
                        })
                        .ToList(),
                }
            );

        List<Post> posts = domainPosts.Select(PostReplicationMapper.ToApp).ToList();

        foreach (Post post in posts)
        {
            changesByPost.TryGetValue(post.Id, out List<PostChangeRow>? postChanges);

            post.Changes = postChanges is null
                ? []
                : ToModelChanges(postChanges, fieldsByChange, post);
        }

        return posts;
    }

    private static async Task<List<T>> ReadList<T>(string path)
    {
        if (!File.Exists(path))
            return [];

        string content = await File.ReadAllTextAsync(path);
        return JsonConvert.DeserializeObject<List<T>>(content) ?? [];
    }

    private static async Task WriteList(string path, object data, bool formatted)
    {
        string targetPath = formatted ? UtilsPath.GetPathFormatted(path) : path;
        string? directory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = formatted
            ? JsonConvert.SerializeObject(data, Formatting.Indented)
            : JsonConvert.SerializeObject(data);

        await File.WriteAllTextAsync(targetPath, json);
    }
}
