namespace Backup.App.Models.Config.Request;

public class Query
{
    public required Dictionary<string, object?> Variables { get; set; }
    public required Dictionary<string, bool> Features { get; set; }
    public required Dictionary<string, bool> FieldToggles { get; set; }

    public Query Clone() =>
        new()
        {
            Variables = (Variables ?? []).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Features = (Features ?? []).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            FieldToggles = (FieldToggles ?? []).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
}
