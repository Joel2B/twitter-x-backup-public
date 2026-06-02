using Backup.Application.Core;
using Backup.Infrastructure.Core.Data;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Dump.Data;

public class DumpDataMultiStore(
    IEnumerable<IDumpDataStore> stores,
    IPrimarySelectionService primarySelectionService,
    ISecondaryStoreSelectionService secondaryStoreSelectionService
) : IDumpData
{
    private readonly DefaultStoreGroup<IDumpDataStore> _storeGroup = new(
        stores,
        primarySelectionService,
        secondaryStoreSelectionService,
            "No dump data stores are configured.",
            "Only one dump data store can be marked as default."
        );

    private IDumpDataStore Primary => _storeGroup.Primary;

    public string? Id
    {
        get => Primary.Id;
        set => Primary.Id = value;
    }

    public Task<DumpData?> GetData(
        ApiContext context,
        CancellationToken cancellationToken = default
    ) => Primary.GetData(context, cancellationToken);

    public async Task Save(
        string response,
        List<Post> posts,
        string cursor,
        ApiContext context,
        CancellationToken cancellationToken = default
    )
    {
        IDumpDataStore primary = Primary;
        await primary.Save(response, posts, cursor, context, cancellationToken);

        foreach (IDumpDataStore store in _storeGroup.GetSecondaries(primary))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.Save(response, posts, cursor, context, cancellationToken);
        }
    }

    public Task Flush(
        IPostDomainData postData,
        string userId,
        ApiContext context,
        CancellationToken cancellationToken = default
    ) => Primary.Flush(postData, userId, context, cancellationToken);
}
