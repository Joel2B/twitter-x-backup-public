namespace Backup.App.Models.Bulk;

public class Source
{
    public required string Link { get; set; }
    public required string UserName { get; set; }
    public SourceType Type { get; set; } = SourceType.None;
}

public enum SourceType
{
    None,
    Media,
    Status,
    Notifications,
}
