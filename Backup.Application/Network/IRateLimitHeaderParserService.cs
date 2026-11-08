using Backup.Application.Network.Models;

namespace Backup.Application.Network;

public interface IRateLimitHeaderParserService
{
    bool TryParse(
        string? rawLimit,
        string? rawRemaining,
        string? rawReset,
        out RateLimitHeaders? headers
    );
}
