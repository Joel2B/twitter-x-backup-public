using Backup.Application.Network.Models;

namespace Backup.Application.Network;

public sealed class RateLimitHeaderParserService : IRateLimitHeaderParserService
{
    public RateLimitHeaders Parse(string? rawLimit, string? rawRemaining, string? rawReset)
    {
        if (!int.TryParse(rawLimit, out int limit))
            throw new FormatException("Invalid x-rate-limit-limit header value.");

        if (!int.TryParse(rawRemaining, out int remaining))
            throw new FormatException("Invalid x-rate-limit-remaining header value.");

        if (!int.TryParse(rawReset, out int reset))
            throw new FormatException("Invalid x-rate-limit-reset header value.");

        return new RateLimitHeaders
        {
            Limit = limit,
            Remaining = remaining,
            ResetUnixSeconds = reset,
        };
    }
}
