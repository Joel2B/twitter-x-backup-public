namespace Backup.App.Models.Config.Data;

public class Data
{
    public required Dictionary<string, string> Aliases { get; set; }
    public required List<Partition> Partitions { get; set; }
    public required List<Post.Storage> Post { get; set; }
    public required List<Dump.Storage> Dump { get; set; }
    public required List<Bulk.Storage> Bulk { get; set; }
    public required List<Media.Storage> Media { get; set; }
    public required List<Backup.Storage> Backup { get; set; }
}
