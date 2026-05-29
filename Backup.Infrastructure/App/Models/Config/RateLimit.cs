namespace Backup.App.Models.Config;

public class RateLimit
{
    public bool Enabled { get; set; }
    public int ThresholdRemaining { get; set; }
    public required RateLimitWait Wait { get; set; }
}

public class RateLimitWait
{
    public int Min { get; set; }
    public int Max { get; set; }
    public bool Reset { get; set; }
}
