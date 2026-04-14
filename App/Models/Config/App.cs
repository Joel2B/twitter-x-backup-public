namespace Backup.App.Models.Config;

public class App
{
    public required Fetch Fetch { get; set; }
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
    public required Network Network { get; set; }

    public Source Source => Fetch.Current;
    public List<Source> Sources => Fetch.Sources;
}
