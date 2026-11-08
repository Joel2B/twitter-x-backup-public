using Backup.Application.Network.Models;

namespace Backup.Application.Network;

public sealed class RateLimitHeaderParserService : IRateLimitHeaderParserService
{
    public bool TryParse(
        string? rawLimit,
        string? rawRemaining,
        string? rawReset,
        out RateLimitHeaders? headers
    )
    {
        headers = null;
        if (
            !int.TryParse(rawLimit, out int limit)
            || !int.TryParse(rawRemaining, out int remaining)
            || !int.TryParse(rawReset, out int reset)
        )
            return false;

        headers = new RateLimitHeaders
        {
            Limit = limit,
            Remaining = remaining,
            ResetUnixSeconds = reset,
        };
        return true;
    }
}
