using Backup.Application.Network.Models;

namespace Backup.Application.Network;

public sealed class RateLimitHeaderParserService : IRateLimitHeaderParserService
{
    public RateLimitHeaders Parse(string? rawLimit, string? rawRemaining, string? rawReset)
    {
        if (!int.TryParse(rawLimit, out int limit))
            throw new Exception("no limit");

        if (!int.TryParse(rawRemaining, out int remaining))
            throw new Exception("no remaining");

        if (!int.TryParse(rawReset, out int reset))
            throw new Exception("no reset");

        return new RateLimitHeaders
        {
            Limit = limit,
            Remaining = remaining,
            ResetUnixSeconds = reset,
        };
    }
}
