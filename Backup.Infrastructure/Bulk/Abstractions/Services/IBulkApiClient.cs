using Backup.Infrastructure.Models.Config.Api;
using ParseResult = Backup.Domain.Posts.ParseResult;
using ParseUser = Backup.Domain.Posts.ParseUser;

namespace Backup.Infrastructure.Interfaces.Services.Bulk;

public interface IBulkApiClient
{
    Task<bool> Verify();

    Task<ParseUser?> GetUserByUser(
        IReadOnlyDictionary<string, ApiConfig> api,
        string userName,
        CancellationToken cancellationToken
    );

    Task<ParseResult?> GetUserMedia(
        IReadOnlyDictionary<string, ApiConfig> api,
        string id,
        string origin,
        int count,
        string? cursor,
        CancellationToken cancellationToken
    );
}
