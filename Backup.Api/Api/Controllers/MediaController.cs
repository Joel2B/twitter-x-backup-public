using Backup.Api.Models;
using Backup.Api.Routing;
using Backup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Root + "/media")]
[Produces("application/json")]
public sealed class MediaController(
    MediaOperationsService operationsService,
    MediaQueryService queryService
) : ControllerBase
{
    private readonly MediaOperationsService _operationsService = operationsService;
    private readonly MediaQueryService _queryService = queryService;

    [HttpPost("run")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Run(CancellationToken cancellationToken) =>
        Ok(await _operationsService.Run(cancellationToken));

    [HttpGet("summary")]
    [ProducesResponseType(typeof(MediaQuerySummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaQuerySummaryResponse>> GetSummary(
        [FromQuery] bool filteredOnly = true,
        CancellationToken cancellationToken = default
    ) => Ok(await _queryService.GetSummary(filteredOnly, cancellationToken));

    [HttpGet("inputs")]
    [ProducesResponseType(typeof(PagedResponse<MediaInputSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<MediaInputSummary>>> GetInputs(
        [FromQuery] MediaInputsQuery request,
        CancellationToken cancellationToken
    ) => Ok(await _queryService.GetInputs(request, cancellationToken));

    [HttpGet("downloads")]
    [ProducesResponseType(
        typeof(PagedResponse<MediaDownloadGroupSummary>),
        StatusCodes.Status200OK
    )]
    public async Task<ActionResult<PagedResponse<MediaDownloadGroupSummary>>> GetDownloads(
        [FromQuery] MediaDownloadsQuery request,
        CancellationToken cancellationToken
    ) => Ok(await _queryService.GetDownloads(request, cancellationToken));

    [HttpGet("downloads/{downloadId}")]
    [ProducesResponseType(typeof(MediaDownloadGroupSummary), StatusCodes.Status200OK)]
    public async Task<ActionResult<MediaDownloadGroupSummary>> GetDownload(
        string downloadId,
        [FromQuery] bool filteredOnly = true,
        CancellationToken cancellationToken = default
    ) => Ok(await _queryService.GetDownload(downloadId, filteredOnly, cancellationToken));

    [HttpGet("files")]
    [ProducesResponseType(typeof(PagedResponse<MediaFileSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<MediaFileSummary>>> GetFiles(
        [FromQuery] MediaFilesQuery request,
        CancellationToken cancellationToken
    ) => Ok(await _queryService.GetFiles(request, cancellationToken));

    [HttpGet("storages")]
    [ProducesResponseType(typeof(IReadOnlyList<MediaStorageSummary>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<MediaStorageSummary>> GetStorages() =>
        Ok(_operationsService.GetStorages());

    [HttpPost("storage/{storageId}/pipeline")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> RunStoragePipeline(
        string storageId,
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.RunStoragePipeline(storageId, cancellationToken));

    [HttpPost("storage/{storageId}/prune")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> PruneStorage(
        string storageId,
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.PruneStorage(storageId, cancellationToken));

    [HttpPost("storage/{storageId}/check-data")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> CheckStorageData(
        string storageId,
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.CheckStorageData(storageId, cancellationToken));

    [HttpPost("storage/{storageId}/check-integrity")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> CheckStorageIntegrity(
        string storageId,
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.CheckStorageIntegrity(storageId, cancellationToken));

    [HttpPost("storage/{storageId}/download")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> DownloadToStorage(
        string storageId,
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.DownloadToStorage(storageId, cancellationToken));

    [HttpPost("storage/{storageId}/replicate")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> ReplicateFromStorage(
        string storageId,
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.ReplicateFromStorage(storageId, cancellationToken));

    [HttpPost("backups/run")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> RunBackups(
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.RunBackups(cancellationToken));
}
