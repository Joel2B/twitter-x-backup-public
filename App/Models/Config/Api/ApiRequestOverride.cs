namespace Backup.App.Models.Config.Api;

public class ApiRequestOverride
{
    public string? Url { get; set; }
    public ApiRequestOverrideQuery? Query { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}

public class ApiRequestOverrideQuery
{
    public Dictionary<string, object?>? Variables { get; set; }
    public Dictionary<string, bool>? Features { get; set; }
    public Dictionary<string, bool>? FieldToggles { get; set; }
}
