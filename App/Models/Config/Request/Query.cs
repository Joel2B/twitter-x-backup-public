namespace Backup.App.Models.Config.Request;

public class Query
{
    public required Dictionary<string, object?> Variables { get; set; }
    public required Dictionary<string, bool> Features { get; set; }
    public required Dictionary<string, bool> FieldToggles { get; set; }

    public Query Clone() =>
        new()
        {
            Variables = Variables,
            Features = Features,
            FieldToggles = FieldToggles,
        };
}
