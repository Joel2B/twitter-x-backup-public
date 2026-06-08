using Backup.Api.Models;
using Backup.Api.Routing;
using Backup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Root + "/partitions")]
[Produces("application/json")]
public sealed class PartitionsController(PartitionOperationsService operationsService)
    : ControllerBase
{
    private readonly PartitionOperationsService _operationsService = operationsService;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PartitionSummary>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PartitionSummary>> Get() => Ok(_operationsService.GetAll());

    [HttpGet("cache")]
    [ProducesResponseType(typeof(IReadOnlyList<PartitionSummary>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PartitionSummary>> GetCache() =>
        Ok(_operationsService.GetCache());

    [HttpGet("primary")]
    [ProducesResponseType(typeof(PartitionSummary), StatusCodes.Status200OK)]
    public ActionResult<PartitionSummary> GetPrimary() => Ok(_operationsService.GetPrimary());

    [HttpGet("heavy")]
    [ProducesResponseType(typeof(PartitionSummary), StatusCodes.Status200OK)]
    public ActionResult<PartitionSummary> GetHeavy() => Ok(_operationsService.GetHeavy());
}
