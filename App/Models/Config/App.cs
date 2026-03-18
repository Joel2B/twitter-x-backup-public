namespace Backup.App.Models.Config;

public class App
{
    public required Source Source { get; set; }
    public required List<Source> Sources { get; set; }
    public required Dictionary<string, Request.Request> Api { get; set; }
    public required Post Post { get; set; }
    public required Data.Data Data { get; set; }
    public required Downloads.Downloads Downloads { get; set; }
    public required Medias.Medias Medias { get; set; }
    public required Proxy.Proxy Proxy { get; set; }
    public required Debug Debug { get; set; }
    public required Tasks.Tasks Tasks { get; set; }
    public required Dump Dump { get; set; }
    public required Bulk Bulk { get; set; }
    public required RateLimit RateLimit { get; set; }
}
