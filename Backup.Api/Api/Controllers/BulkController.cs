using Backup.Api.Models;
using Backup.Api.Routing;
using Backup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Root + "/bulk")]
[Produces("application/json")]
public sealed class BulkController(BulkOperationsService operationsService) : ControllerBase
{
    private readonly BulkOperationsService _operationsService = operationsService;

    [HttpPost("run")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationResult>> Run(
        [FromBody] BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await _operationsService.Run(request, cancellationToken));
    }

    [HttpPost("import")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationResult>> Import(
        [FromBody] BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await _operationsService.Import(request, cancellationToken));
    }

    [HttpPost("verify")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Verify(CancellationToken cancellationToken) =>
        Ok(await _operationsService.Verify(cancellationToken));

    [HttpPost("phase1")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationResult>> Phase1(
        [FromBody] BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await _operationsService.Phase1(request, cancellationToken));
    }

    [HttpPost("phase2")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationResult>> Phase2(
        [FromBody] BulkRunRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await _operationsService.Phase2(request, cancellationToken));
    }

    [HttpPost("phase2-reset")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> ResetPhase2(
        CancellationToken cancellationToken
    ) => Ok(await _operationsService.ResetPhase2(cancellationToken));

    [HttpPost("prune")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Prune(CancellationToken cancellationToken) =>
        Ok(await _operationsService.Prune(cancellationToken));
}
