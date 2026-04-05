namespace Backup.App.Models.Post;

public class Change
{
    public required string UserId { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public Data? Data { get; set; }
    public Dictionary<string, IndexData>? Index { get; set; }

    public Change Clone() =>
        new()
        {
            UserId = UserId,
            Date = Date,
            Data = Data?.Clone(),
            Index = Index?.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Clone(),
                StringComparer.Ordinal
            ),
        };
}
