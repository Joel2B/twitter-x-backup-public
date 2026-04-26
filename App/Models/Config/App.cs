namespace Backup.App.Models.Config;

public class App
{
    public required Dictionary<string, Api.Api> Api { get; set; }
    public required Dictionary<string, FetchItem> Fetch { get; set; }
    public required Services Services { get; set; }
    public required Data.Data Data { get; set; }
    public required Downloads.Downloads Downloads { get; set; }
    public required Medias.Medias Medias { get; set; }
    public required Proxy.Proxy Proxy { get; set; }
    public required Debug Debug { get; set; }
    public required Tasks.Tasks Tasks { get; set; }
    public required Bulk Bulk { get; set; }
    public required Network Network { get; set; }
}
