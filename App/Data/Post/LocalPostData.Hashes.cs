using Backup.App.Models.Data.Json;
using Newtonsoft.Json;

namespace Backup.App.Data.Post;

public partial class LocalPostData
{
    private async Task<Dictionary<string, PostMetaRow>> GetPostMetaCache()
    {
        if (_postMetaCache is not null)
            return _postMetaCache;

        PrepareTablesDirectories();
        string postMetaPath = GetCurrentTablesFilePath(PostMetaFileName);

        if (File.Exists(postMetaPath))
        {
            string content = await File.ReadAllTextAsync(postMetaPath);

            List<PostMetaRow> rows =
                JsonConvert.DeserializeObject<List<PostMetaRow>>(content) ?? [];

            _postMetaCache = rows.Where(row =>
                    !string.IsNullOrWhiteSpace(row.Id) && !string.IsNullOrWhiteSpace(row.Hash)
                )
                .GroupBy(row => row.Id, StringComparer.Ordinal)
                .Select(group => group.Last())
                .ToDictionary(row => row.Id, row => row, StringComparer.Ordinal);

            return _postMetaCache;
        }

        _postMetaCache = [];
        return _postMetaCache;
    }

    private async Task<Dictionary<string, PostMetaRow>> EnsurePostMetaCache(
        IEnumerable<Models.Post.Post> posts
    )
    {
        Dictionary<string, PostMetaRow> meta = await GetPostMetaCache();
        HashSet<string> ids = [];

        foreach (Models.Post.Post post in posts)
        {
            ids.Add(post.Id);

            if (!meta.TryGetValue(post.Id, out PostMetaRow? value))
            {
                meta[post.Id] = new()
                {
                    Id = post.Id,
                    Hash = ComputePostHash(post),
                    Deleted = post.Deleted,
                };

                continue;
            }

            value.Deleted = post.Deleted;

            if (string.IsNullOrWhiteSpace(value.Hash))
                value.Hash = ComputePostHash(post);
        }

        List<string> staleIds = [.. meta.Keys.Where(id => !ids.Contains(id))];

        foreach (string staleId in staleIds)
            meta.Remove(staleId);

        return meta;
    }

    private static string ComputePostHash(Models.Post.Post post)
    {
        return Utils.PostHash.Compute(post);
    }
}
