namespace Backup.Domain.Posts;

public class PostStoreParityResult
{
    public string PrimaryLabel { get; set; } = "";
    public List<PostStoreSnapshot> Snapshots { get; set; } = [];
    public List<PostStoreMismatch> Mismatches { get; set; } = [];
}

public class PostStoreSnapshot
{
    public string Label { get; set; } = "";
    public PostStoreCounts Counts { get; set; } = new();
}

public class PostStoreMismatch
{
    public string PrimaryLabel { get; set; } = "";
    public string SecondaryLabel { get; set; } = "";
    public List<string> Diffs { get; set; } = [];
}
