namespace Backup.App.Models.Config.Request;

public class Request
{
    public bool Enabled { get; set; } = true;
    public required string Url { get; set; }
    public required Query Query { get; set; }
    public required Dictionary<string, string> Headers { get; set; }

    public Request Clone() =>
        new()
        {
            Enabled = Enabled,
            Url = Url,
            Query = Query.Clone(),
            Headers = Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
}
