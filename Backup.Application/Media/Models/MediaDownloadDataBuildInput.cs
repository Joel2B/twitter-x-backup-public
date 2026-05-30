namespace Backup.Application.Media.Models;

public sealed class MediaDownloadDataBuildInput
{
    public string? Id { get; set; }
    public required string PostId { get; set; }
    public required string Url { get; set; }
    public required string MediaType { get; set; }
    public List<string> MidPath { get; set; } = [];
    public required string FormatType { get; set; }
    public string? ResolutionType { get; set; }
    public required string Name { get; set; }
    public bool IncludeQuery { get; set; } = true;
}
