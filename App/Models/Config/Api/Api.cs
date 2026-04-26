namespace Backup.App.Models.Config.Api;

public class Api
{
    public required string Id { get; set; }
    public bool Enabled { get; set; } = true;
    public required Request.Request Request { get; set; }
}
