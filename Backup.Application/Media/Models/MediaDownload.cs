namespace Backup.Application.Media.Models;

public sealed class MediaDownload
{
    public required string Id { get; set; }
    public required List<MediaDownloadData> Data { get; set; }

    public MediaDownload Clone() =>
        new() { Id = Id, Data = Data.Select(item => item.Clone()).ToList() };
}

public sealed class MediaDownloadData
{
    public required string Url { get; set; }
    public required string Path { get; set; }

    public MediaDownloadData Clone() => new() { Url = Url, Path = Path };
}
