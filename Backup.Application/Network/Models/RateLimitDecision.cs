namespace Backup.Application.Network.Models;

public sealed class RateLimitDecision
{
    public bool Continue { get; set; }
    public int Threshold { get; set; }
    public int WaitMilliseconds { get; set; }
}
