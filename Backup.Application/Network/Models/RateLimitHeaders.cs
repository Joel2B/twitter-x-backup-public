namespace Backup.Application.Network.Models;

public sealed class RateLimitHeaders
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public int ResetUnixSeconds { get; set; }
}
