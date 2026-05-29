namespace Backup.App.Models.Media.Processors;

public class Data
{
    public string? Id { get; set; }
    public required string PostId { get; set; }
    public required string Url { get; set; }
    public required string MediaType { get; set; }
    public List<string> MidPath = [];
    public required string FormatType { get; set; }
    public string? ResolutionType { get; set; }
    public required string Name { get; set; }
    public ImageOptions Options = new();
}

public class ImageOptions
{
    public bool Query = true;
}

public class Resolution
{
    public required string Name { get; set; }
    public required string Type { get; set; }
}
