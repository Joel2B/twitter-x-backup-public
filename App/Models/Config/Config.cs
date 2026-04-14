namespace Backup.App.Models.Config;

public class Source
{
    public required string Id { get; set; }
    public required bool Enabled { get; set; }
    public required Request.Request Request { get; set; }
    public required int Count { get; set; }

    public Source Clone() =>
        new()
        {
            Id = Id,
            Enabled = Enabled,
            Request = Request.Clone(),
            Count = Count,
        };
}
