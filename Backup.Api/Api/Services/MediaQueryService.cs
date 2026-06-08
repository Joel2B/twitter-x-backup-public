using Backup.Api.Errors;
using Backup.Api.Models;
using Backup.Application.Media.Models;
using Backup.Application.Media.Ports;
using Backup.Domain.Posts;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models;

namespace Backup.Api.Services;

public sealed class MediaQueryService(
    IMediaOrchestrationCommand mediaOrchestrationCommand,
    IEnumerable<IMediaStorage> mediaStorage
)
{
    private readonly IMediaOrchestrationCommand _mediaOrchestrationCommand =
        mediaOrchestrationCommand;
    private readonly IReadOnlyDictionary<string, IMediaStorage> _storageById = mediaStorage
        .Where(storage => !string.IsNullOrWhiteSpace(storage.Id))
        .ToDictionary(storage => storage.Id!, storage => storage, StringComparer.OrdinalIgnoreCase);

    public async Task<MediaQuerySummaryResponse> GetSummary(
        bool filteredOnly,
        CancellationToken cancellationToken
    )
    {
        MediaProjectionData projection = await LoadProjection(filteredOnly, cancellationToken);

        return new MediaQuerySummaryResponse
        {
            FilteredOnly = filteredOnly,
            MediaInputCount = projection.Inputs.Count,
            DownloadCount = projection.Downloads.Count,
            FileCount = projection.Downloads.Sum(download => download.Data.Count),
            StorageCount = _storageById.Count,
            Storages = _storageById
                .Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
    }

    public async Task<PagedResponse<MediaInputSummary>> GetInputs(
        MediaInputsQuery request,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<MediaInput> inputs = await _mediaOrchestrationCommand.GetMediaInputs(
            cancellationToken
        );

        IEnumerable<MediaInput> query = inputs;

        if (!string.IsNullOrWhiteSpace(request.PostId))
            query = query.Where(item =>
                string.Equals(item.Id, request.PostId, StringComparison.Ordinal)
            );

        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            query = query.Where(item =>
                string.Equals(item.Profile.Id, request.ProfileId, StringComparison.Ordinal)
            );
        }

        if (request.Deleted.HasValue)
            query = query.Where(item => item.Deleted == request.Deleted.Value);

        List<MediaInputSummary> items = query
            .OrderByDescending(item => item.Id, StringComparer.Ordinal)
            .Select(item => new MediaInputSummary
            {
                PostId = item.Id,
                ProfileId = item.Profile.Id,
                UserName = item.Profile.UserName,
                DisplayName = item.Profile.Name,
                Deleted = item.Deleted,
                MediaCount = item.Medias?.Count ?? 0,
                MediaTypes =
                    item.Medias?.Select(media => media.Type)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(type => type, StringComparer.Ordinal)
                        .ToList() ?? [],
                HasProfileImage = !string.IsNullOrWhiteSpace(item.Profile.ImageUrl),
                HasBannerImage = !string.IsNullOrWhiteSpace(item.Profile.BannerUrl),
            })
            .ToList();

        return Page(items, request.Page, request.PageSize);
    }

    public async Task<PagedResponse<MediaDownloadGroupSummary>> GetDownloads(
        MediaDownloadsQuery request,
        CancellationToken cancellationToken
    )
    {
        MediaProjectionData projection = await LoadProjection(
            request.FilteredOnly,
            cancellationToken
        );
        IEnumerable<MediaDownload> query = projection.Downloads;

        if (!string.IsNullOrWhiteSpace(request.PostId))
            query = query.Where(item =>
                string.Equals(item.Id, request.PostId, StringComparison.Ordinal)
            );

        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            query = query.Where(item =>
                projection.InputsById.TryGetValue(item.Id, out MediaInput? input)
                && string.Equals(input.Profile.Id, request.ProfileId, StringComparison.Ordinal)
            );
        }

        List<MediaDownload> downloads = query
            .OrderByDescending(item => item.Id, StringComparer.Ordinal)
            .ToList();

        PagedResponse<MediaDownload> page = Page(downloads, request.Page, request.PageSize);
        List<MediaDownloadGroupSummary> items = [];

        foreach (MediaDownload download in page.Items)
            items.Add(
                await BuildDownloadSummary(download, projection.InputsById, cancellationToken)
            );

        return new PagedResponse<MediaDownloadGroupSummary>
        {
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
            HasPrevious = page.HasPrevious,
            HasNext = page.HasNext,
            Items = items,
        };
    }

    public async Task<MediaDownloadGroupSummary> GetDownload(
        string downloadId,
        bool filteredOnly,
        CancellationToken cancellationToken
    )
    {
        MediaProjectionData projection = await LoadProjection(filteredOnly, cancellationToken);
        MediaDownload? download = projection.Downloads.FirstOrDefault(item =>
            string.Equals(item.Id, downloadId, StringComparison.Ordinal)
        );

        if (download is null)
            throw new ApiException($"media download '{downloadId}' was not found.");

        return await BuildDownloadSummary(download, projection.InputsById, cancellationToken);
    }

    public async Task<PagedResponse<MediaFileSummary>> GetFiles(
        MediaFilesQuery request,
        CancellationToken cancellationToken
    )
    {
        MediaProjectionData projection = await LoadProjection(
            request.FilteredOnly,
            cancellationToken
        );
        IEnumerable<FlatMediaFile> query = projection.Downloads.SelectMany(download =>
            download.Data.Select(data => new FlatMediaFile(download.Id, data))
        );

        if (!string.IsNullOrWhiteSpace(request.PostId))
            query = query.Where(item =>
                string.Equals(item.PostId, request.PostId, StringComparison.Ordinal)
            );

        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            query = query.Where(item =>
                projection.InputsById.TryGetValue(item.PostId, out MediaInput? input)
                && string.Equals(input.Profile.Id, request.ProfileId, StringComparison.Ordinal)
            );
        }

        if (!string.IsNullOrWhiteSpace(request.PathContains))
        {
            query = query.Where(item =>
                item.Data.Path.Contains(request.PathContains, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (!string.IsNullOrWhiteSpace(request.UrlContains))
        {
            query = query.Where(item =>
                item.Data.Url.Contains(request.UrlContains, StringComparison.OrdinalIgnoreCase)
            );
        }

        List<FlatMediaFile> files = query
            .OrderByDescending(item => item.PostId, StringComparer.Ordinal)
            .ThenBy(item => item.Data.Path, StringComparer.Ordinal)
            .ToList();

        PagedResponse<FlatMediaFile> page = Page(files, request.Page, request.PageSize);
        List<MediaFileSummary> items = [];

        foreach (FlatMediaFile file in page.Items)
        {
            projection.InputsById.TryGetValue(file.PostId, out MediaInput? input);
            items.Add(await BuildFileSummary(file.PostId, input, file.Data, cancellationToken));
        }

        return new PagedResponse<MediaFileSummary>
        {
            Page = page.Page,
            PageSize = page.PageSize,
            TotalItems = page.TotalItems,
            TotalPages = page.TotalPages,
            HasPrevious = page.HasPrevious,
            HasNext = page.HasNext,
            Items = items,
        };
    }

    private async Task<MediaProjectionData> LoadProjection(
        bool filteredOnly,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<MediaInput> inputs = await _mediaOrchestrationCommand.GetMediaInputs(
            cancellationToken
        );
        MediaProcessingResult processed = await _mediaOrchestrationCommand.Process(
            inputs,
            cancellationToken
        );
        List<MediaDownload> all = processed.All.Select(item => item.Clone()).ToList();
        List<MediaDownload> filtered = processed.Filtered.Select(item => item.Clone()).ToList();

        await _mediaOrchestrationCommand.Prune(all, cancellationToken);
        await _mediaOrchestrationCommand.Filter(filtered, cancellationToken);

        return new MediaProjectionData(
            inputs.ToList(),
            inputs.ToDictionary(item => item.Id, StringComparer.Ordinal),
            filteredOnly ? filtered : all
        );
    }

    private async Task<MediaDownloadGroupSummary> BuildDownloadSummary(
        MediaDownload download,
        IReadOnlyDictionary<string, MediaInput> inputsById,
        CancellationToken cancellationToken
    )
    {
        inputsById.TryGetValue(download.Id, out MediaInput? input);

        List<MediaFileSummary> items = [];

        foreach (
            MediaDownloadData data in download.Data.OrderBy(
                item => item.Path,
                StringComparer.Ordinal
            )
        )
            items.Add(await BuildFileSummary(download.Id, input, data, cancellationToken));

        return new MediaDownloadGroupSummary
        {
            PostId = download.Id,
            ProfileId = input?.Profile.Id,
            UserName = input?.Profile.UserName,
            DisplayName = input?.Profile.Name,
            Deleted = input?.Deleted,
            FileCount = items.Count,
            Items = items,
        };
    }

    private async Task<MediaFileSummary> BuildFileSummary(
        string postId,
        MediaInput? input,
        MediaDownloadData data,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<MediaStorageFileState> storages = [];

        foreach (
            (string storageId, IMediaStorage storage) in _storageById.OrderBy(
                entry => entry.Key,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            MediaCacheEntry? cache = await storage.GetCache(data.Path);

            storages.Add(
                new MediaStorageFileState
                {
                    StorageId = storageId,
                    Exists = cache is not null,
                    PartitionId = cache?.PartitionId,
                    StreamSize = cache?.Size?.Stream,
                    FileSize = cache?.Size?.File,
                }
            );
        }

        return new MediaFileSummary
        {
            PostId = postId,
            ProfileId = input?.Profile.Id,
            UserName = input?.Profile.UserName,
            Url = data.Url,
            Path = data.Path,
            Storages = storages,
        };
    }

    private static PagedResponse<T> Page<T>(IReadOnlyList<T> source, int page, int pageSize)
    {
        int normalizedPage = page <= 0 ? 1 : page;
        int normalizedPageSize = Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 250);
        int totalItems = source.Count;
        int totalPages =
            totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
        int skip = (normalizedPage - 1) * normalizedPageSize;
        List<T> items =
            skip >= totalItems ? [] : source.Skip(skip).Take(normalizedPageSize).ToList();

        return new PagedResponse<T>
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasPrevious = normalizedPage > 1 && totalItems > 0,
            HasNext = normalizedPage < totalPages,
            Items = items,
        };
    }

    private sealed record MediaProjectionData(
        List<MediaInput> Inputs,
        IReadOnlyDictionary<string, MediaInput> InputsById,
        List<MediaDownload> Downloads
    );

    private sealed record FlatMediaFile(string PostId, MediaDownloadData Data);
}
