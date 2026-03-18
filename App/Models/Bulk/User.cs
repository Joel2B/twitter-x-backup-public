namespace Backup.App.Models.Bulk;

public class User
{
    public string? Id { get; set; }
    public required string Name { get; set; }
    public required StatusUser Status { get; set; }
}

public enum StatusUser
{
    None,
    Active,
    Inactive,
}
