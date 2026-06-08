using Backup.Api.Models;
using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Api.Services;

public sealed class PostOperationsService(
    ConfigContextResolver contextResolver,
    Backup.Infrastructure.Posts.Abstractions.Services.IPostService postService,
    IPostData postData,
    IPostReplication postReplication,
    IEnumerable<IPostDataStore> postStores,
    Backup.Application.Posts.IPostStoreParityService postStoreParityService,
    Backup.Application.Posts.IPostStoreParityReportService postStoreParityReportService
)
{
    private readonly ConfigContextResolver _contextResolver = contextResolver;
    private readonly Backup.Infrastructure.Posts.Abstractions.Services.IPostService _postService =
        postService;
    private readonly IPostData _postData = postData;
    private readonly IPostReplication _postReplication = postReplication;
    private readonly IReadOnlyList<IPostDataStore> _postStores = [.. postStores];
    private readonly Backup.Application.Posts.IPostStoreParityService _postStoreParityService =
        postStoreParityService;
    private readonly Backup.Application.Posts.IPostStoreParityReportService _postStoreParityReportService =
        postStoreParityReportService;

    public async Task<OperationResult> Download(
        PostDownloadRequest request,
        CancellationToken cancellationToken
    )
    {
        await _postService.Download(
            _contextResolver.GetRequiredApiContext(request.UserId, request.SourceId),
            cancellationToken
        );

        return new OperationResult(
            "post-download",
            "completed",
            $"user={request.UserId}, source={request.SourceId}"
        );
    }

    public async Task<OperationResult> Recovery(
        PostRecoveryRequest request,
        CancellationToken cancellationToken
    )
    {
        await _postService.Recover(
            _contextResolver.GetRequiredUsersContext(request.UserId),
            cancellationToken
        );

        return new OperationResult("post-recovery", "completed", $"user={request.UserId}");
    }

    public async Task<PostStoreParityResponse> GetParity(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<IPostStoreCountSource> adapters = _postStores
            .Select(store => (IPostStoreCountSource)new PostStoreCountSource(store))
            .ToList();

        Backup.Domain.Posts.PostStoreParityResult parity = await _postStoreParityService.Verify(
            adapters
        );
        PostStoreParityReport report = _postStoreParityReportService.Build(parity);

        return new PostStoreParityResponse
        {
            VerifiedAt = DateTimeOffset.UtcNow,
            StoreCount = adapters.Count,
            Snapshots = report
                .Snapshots.Select(snapshot => new PostStoreParitySnapshot
                {
                    Label = snapshot.Label,
                    Posts = snapshot.Posts,
                    Profiles = snapshot.Profiles,
                    Hashtags = snapshot.Hashtags,
                    Medias = snapshot.Medias,
                    MediaVariants = snapshot.MediaVariants,
                    IndexEntries = snapshot.IndexEntries,
                    Changes = snapshot.Changes,
                    ChangeFields = snapshot.ChangeFields,
                    HashMeta = snapshot.HashMeta,
                })
                .ToList(),
            Statuses = report
                .Statuses.Select(status => new PostStoreParityStatus
                {
                    PrimaryLabel = status.PrimaryLabel,
                    SecondaryLabel = status.SecondaryLabel,
                    IsMismatch = status.IsMismatch,
                    DiffsText = status.DiffsText,
                })
                .ToList(),
        };
    }

    public async Task<PostCountsResponse> GetCounts()
    {
        int primaryCount = await _postData.GetCount();
        List<PostStoreCountsSummary> stores = [];

        foreach (IPostDataStore store in _postStores)
        {
            PostStoreCounts counts = await store.GetStoreCounts();
            stores.Add(MapStoreCounts(store, counts));
        }

        return new PostCountsResponse { PrimaryCount = primaryCount, Stores = stores };
    }

    public IReadOnlyList<PostStoreSummary> GetStores() =>
        _postStores
            .Select(store => new PostStoreSummary
            {
                Id = string.IsNullOrWhiteSpace(store.Id) ? store.GetType().Name : store.Id,
                IsDefault = store.IsDefault,
                StoreType = store.GetType().Name,
            })
            .ToList();

    public Task<List<Post>> GetByIds(PostIdsRequest request) => _postData.GetByIds(request.Ids);

    public async Task<IReadOnlyList<MediaInput>> GetMediaInputs()
    {
        List<MediaInput>? inputs = await _postData.GetMediaInputs();
        return inputs ?? [];
    }

    public async Task<OperationResult> Save()
    {
        await _postData.Save();
        return new OperationResult("post-save", "completed");
    }

    public async Task<OperationResult> Prune()
    {
        await _postData.Prune();
        return new OperationResult("post-prune", "completed");
    }

    public async Task<OperationResult> Replicate()
    {
        if (_postStores.Count <= 1)
            return new OperationResult(
                "post-replication",
                "skipped",
                "only one post store is enabled"
            );

        await _postReplication.Replicate(_postStores);
        return new OperationResult("post-replication", "completed", $"stores={_postStores.Count}");
    }

    private static PostStoreCountsSummary MapStoreCounts(
        IPostDataStore store,
        PostStoreCounts counts
    ) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(store.Id) ? store.GetType().Name : store.Id,
            IsDefault = store.IsDefault,
            Posts = counts.Posts,
            Profiles = counts.Profiles,
            Hashtags = counts.Hashtags,
            Medias = counts.Medias,
            MediaVariants = counts.MediaVariants,
            IndexEntries = counts.IndexEntries,
            Changes = counts.Changes,
            ChangeFields = counts.ChangeFields,
            HashMeta = counts.HashMeta,
        };

    private sealed class PostStoreCountSource(IPostDataStore store) : IPostStoreCountSource
    {
        private readonly IPostDataStore _store = store;

        public string Label =>
            string.IsNullOrWhiteSpace(_store.Id) ? _store.GetType().Name : _store.Id!;

        public bool IsDefault => _store.IsDefault;

        public async Task<Backup.Domain.Posts.PostStoreCounts> GetStoreCounts()
        {
            PostStoreCounts counts = await _store.GetStoreCounts();

            return new Backup.Domain.Posts.PostStoreCounts
            {
                Posts = counts.Posts,
                Profiles = counts.Profiles,
                Hashtags = counts.Hashtags,
                Medias = counts.Medias,
                MediaVariants = counts.MediaVariants,
                IndexEntries = counts.IndexEntries,
                Changes = counts.Changes,
                ChangeFields = counts.ChangeFields,
                HashMeta = counts.HashMeta,
            };
        }
    }
}
