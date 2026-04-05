using Backup.App.Models.Data.Json;
using Newtonsoft.Json;

namespace Backup.App.Data.Post;

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

    private static string GetUniqueHistoryDirectoryPath(string basePath)
    {
        DateTime candidate = DateTime.Now;

        while (true)
        {
            string path = Path.Combine(basePath, candidate.ToString(LegacyDateFormat));

            if (!Directory.Exists(path) && !File.Exists(path))
                return path;

            candidate = candidate.AddSeconds(1);
        }
    }

    private static LocalPostTables BuildTables(List<Models.Post.Post> posts)
    {
        LocalPostTables tables = new();
        Dictionary<string, ProfileRow> profileById = new(StringComparer.Ordinal);

        foreach (Models.Post.Post post in posts)
        {
            profileById[post.Profile.Id] = new()
            {
                Id = post.Profile.Id,
                UserName = post.Profile.UserName,
                Name = post.Profile.Name,
                BannerUrl = post.Profile.BannerUrl,
                ImageUrl = post.Profile.ImageUrl,
                Following = post.Profile.Following,
                CountMedia = post.Profile.Count?.Media,
            };

            tables.Posts.Add(
                new()
                {
                    Id = post.Id,
                    ProfileId = post.Profile.Id,
                    Description = post.Description,
                    Retweeted = post.Retweeted,
                    Favorited = post.Favorited,
                    Bookmarked = post.Bookmarked,
                    CreatedAt = post.CreatedAt,
                    Deleted = post.Deleted,
                }
            );

            if (post.Hashtags is not null)
                for (int i = 0; i < post.Hashtags.Count; i++)
                    tables.Hashtags.Add(
                        new()
                        {
                            PostId = post.Id,
                            Value = post.Hashtags[i],
                            Ordinal = i,
                        }
                    );

            if (post.Medias is not null)
            {
                for (int i = 0; i < post.Medias.Count; i++)
                {
                    Models.Post.Media media = post.Medias[i];

                    tables.Medias.Add(
                        new()
                        {
                            PostId = post.Id,
                            Ordinal = i,
                            MediaId = media.Id,
                            Url = media.Url,
                            Type = media.Type,
                            VideoDurationMilis = media.VideoInfo?.DurationMilis,
                        }
                    );

                    if (media.VideoInfo?.Variants is null)
                        continue;

                    for (int j = 0; j < media.VideoInfo.Variants.Count; j++)
                    {
                        Models.Post.Variant variant = media.VideoInfo.Variants[j];

                        tables.MediaVariants.Add(
                            new()
                            {
                                PostId = post.Id,
                                MediaOrdinal = i,
                                Ordinal = j,
                                ContentType = variant.ContentType,
                                Bitrate = variant.Bitrate,
                                Url = variant.Url,
                            }
                        );
                    }
                }
            }

            foreach (var userIndex in post.Index)
            {
                foreach (var originIndex in userIndex.Value)
                {
                    tables.IndexEntries.Add(
                        new()
                        {
                            PostId = post.Id,
                            UserId = userIndex.Key,
                            Origin = originIndex.Key,
                            Previous = originIndex.Value.Previous,
                            Next = originIndex.Value.Next,
                        }
                    );
                }
            }

            AddChangeRows(tables, post);
        }

        tables.Profiles = [.. profileById.Values];
        return tables;
    }

    private static List<Models.Post.Post> BuildPosts(LocalPostTables tables)
    {
        Dictionary<string, Models.Post.Profile> profiles = tables.Profiles.ToDictionary(
            o => o.Id,
            o => new Models.Post.Profile
            {
                Id = o.Id,
                UserName = o.UserName,
                Name = o.Name,
                BannerUrl = o.BannerUrl,
                ImageUrl = o.ImageUrl,
                Following = o.Following,
                Count = o.CountMedia.HasValue
                    ? new Models.Post.Count { Media = o.CountMedia }
                    : null,
            },
            StringComparer.Ordinal
        );

        Dictionary<string, List<string>> hashtagsByPost = tables
            .Hashtags.GroupBy(o => o.PostId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(o => o.Ordinal).Select(o => o.Value).ToList(),
                StringComparer.Ordinal
            );

        Dictionary<(string PostId, int MediaOrdinal), List<Models.Post.Variant>> variantsByMedia =
            tables
                .MediaVariants.GroupBy(o => (o.PostId, o.MediaOrdinal))
                .ToDictionary(
                    g => g.Key,
                    g =>
                        g.OrderBy(o => o.Ordinal)
                            .Select(o => new Models.Post.Variant
                            {
                                ContentType = o.ContentType,
                                Bitrate = o.Bitrate,
                                Url = o.Url,
                            })
                            .ToList()
                );

        Dictionary<string, List<Models.Post.Media>> mediasByPost = tables
            .Medias.GroupBy(o => o.PostId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g =>
                    g.OrderBy(o => o.Ordinal)
                        .Select(o =>
                        {
                            variantsByMedia.TryGetValue((o.PostId, o.Ordinal), out var variants);

                            return new Models.Post.Media
                            {
                                Id = o.MediaId,
                                Url = o.Url,
                                Type = o.Type,
                                VideoInfo =
                                    o.VideoDurationMilis is null
                                    && (variants is null || variants.Count == 0)
                                        ? null
                                        : new Models.Post.VideoInfo
                                        {
                                            DurationMilis = o.VideoDurationMilis,
                                            Variants = variants,
                                        },
                            };
                        })
                        .ToList(),
                StringComparer.Ordinal
            );

        Dictionary<
            string,
            Dictionary<string, Dictionary<string, Models.Post.IndexData>>
        > indexByPost = tables
            .IndexEntries.GroupBy(o => o.PostId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g =>
                    g.GroupBy(o => o.UserId, StringComparer.Ordinal)
                        .ToDictionary(
                            ug => ug.Key,
                            ug =>
                                ug.ToDictionary(
                                    o => o.Origin,
                                    o => new Models.Post.IndexData
                                    {
                                        Previous = o.Previous,
                                        Next = o.Next,
                                    },
                                    StringComparer.Ordinal
                                ),
                            StringComparer.Ordinal
                        ),
                StringComparer.Ordinal
            );

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

        List<Models.Post.Post> posts = new(tables.Posts.Count);

        foreach (PostRow row in tables.Posts)
        {
            Models.Post.Profile profile = profiles.TryGetValue(
                row.ProfileId,
                out Models.Post.Profile? value
            )
                ? value
                : new Models.Post.Profile { Id = row.ProfileId };

            hashtagsByPost.TryGetValue(row.Id, out List<string>? hashtags);
            mediasByPost.TryGetValue(row.Id, out List<Models.Post.Media>? medias);

            indexByPost.TryGetValue(
                row.Id,
                out Dictionary<string, Dictionary<string, Models.Post.IndexData>>? index
            );

            changesByPost.TryGetValue(row.Id, out List<PostChangeRow>? postChanges);

            Models.Post.Post post = new()
            {
                Id = row.Id,
                Profile = profile,
                Description = row.Description,
                Retweeted = row.Retweeted,
                Favorited = row.Favorited,
                Bookmarked = row.Bookmarked,
                CreatedAt = row.CreatedAt,
                Deleted = row.Deleted,
                Hashtags = hashtags is not null && hashtags.Count > 0 ? hashtags : null,
                Medias = medias is not null && medias.Count > 0 ? medias : null,
                Index = index ?? [],
            };

            post.Changes = postChanges is null
                ? []
                : ToModelChanges(postChanges, fieldsByChange, post);

            posts.Add(post);
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
        string targetPath = formatted ? Utils.Path.GetPathFormatted(path) : path;
        string? directory = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string json = formatted
            ? JsonConvert.SerializeObject(data, Formatting.Indented)
            : JsonConvert.SerializeObject(data);

        await File.WriteAllTextAsync(targetPath, json);
    }
}
