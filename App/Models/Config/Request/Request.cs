namespace Backup.App.Models.Config.Request;

public class Request
{
    public required string Url { get; set; }
    public required Query Query { get; set; }
    public required Dictionary<string, string> Headers { get; set; }

    public Request Clone() =>
        new()
        {
            Url = Url,
            Query = Query.Clone(),
            Headers = (Headers ?? []).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
}
