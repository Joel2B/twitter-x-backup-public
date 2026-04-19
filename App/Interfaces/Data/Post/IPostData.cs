namespace Backup.App.Interfaces.Data.Post;

public interface IPostData
{
    public string? Id { get; set; }
    public Task<int> GetCount();
    public Task<List<Models.Post.Post>?> GetAll();
    public Task<List<Models.Post.MediaInput>?> GetMediaInputs();

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    );

    public Task AddPosts(
        string userId,
        string origin,
        List<Models.Post.Post> incoming,
        Models.Post.MergeOptions? options = null
    );

    public async Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    )
    {
        List<Models.Post.Post> posts = await GetAll() ?? [];

        HashSet<string> keep = keepPostIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        int deletedCount = 0;

        foreach (Models.Post.Post post in posts)
        {
            bool hasScope =
                post.Index.TryGetValue(userId, out Dictionary<string, Models.Post.IndexData>? index)
                && index.ContainsKey(origin);

            if (!hasScope)
                continue;

            if (keep.Contains(post.Id) || post.Deleted)
                continue;

            post.Deleted = true;
            deletedCount++;
        }

        return deletedCount;
    }

    public Task Reset(List<Models.Post.Post> posts);
    public Task Save();
    public Task Prune();
}
