namespace Backup.App.Models.Config.Data;

public class Tasks
{
    public required bool Prune { get; set; }
    public bool Verify { get; set; } = false;
    public bool Fix { get; set; } = false;
}
