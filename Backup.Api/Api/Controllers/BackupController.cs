using Backup.Api.Models;
using Backup.Api.Routing;
using Backup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Root + "/backup")]
[Produces("application/json")]
public sealed class BackupController(BackupOperationsService operationsService) : ControllerBase
{
    private readonly BackupOperationsService _operationsService = operationsService;

    [HttpGet("plan")]
    [ProducesResponseType(typeof(BackupPlanResponse), StatusCodes.Status200OK)]
    public ActionResult<BackupPlanResponse> GetPlan() => Ok(_operationsService.GetPlan());

    [HttpPost("run")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Run(CancellationToken cancellationToken) =>
        Ok(await _operationsService.Run(cancellationToken));

    [HttpPost("posts")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> RunPosts(
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.RunPosts(cancellationToken));

    [HttpPost("recovery")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> RunRecovery(
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.RunRecovery(cancellationToken));

    [HttpPost("bulk")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> RunBulk(CancellationToken cancellationToken) =>
        Ok(await _operationsService.RunBulk(cancellationToken));

    [HttpPost("media")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> RunMedia(
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.RunMedia(cancellationToken));

    [HttpPost("verify-post-stores")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> VerifyPostStores(
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.VerifyPostStores(cancellationToken));
}
