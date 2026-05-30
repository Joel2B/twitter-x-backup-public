using Backup.Application.Network.Models;

namespace Backup.Application.Network;

public interface IRateLimitHeaderParserService
{
    RateLimitHeaders Parse(string? rawLimit, string? rawRemaining, string? rawReset);
}
