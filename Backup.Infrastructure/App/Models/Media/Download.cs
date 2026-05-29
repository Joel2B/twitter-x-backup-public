namespace Backup.App.Models.Media;

public class Download
{
    public required string Id { get; set; }
    public required List<DataDownload> Data { get; set; }

    public Download Clone() => new() { Id = Id, Data = [.. Data.Select(data => data.Clone())] };
}

public class DataDownload
{
    public required string Url { get; set; }
    public required string Path { get; set; }

    public DataDownload Clone() => new() { Url = Url, Path = Path };
}
