using Backup.Api.Models;
using Backup.Api.Routing;
using Backup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Root + "/config")]
[Produces("application/json")]
public sealed class ConfigController(ConfigOperationsService operationsService) : ControllerBase
{
    private readonly ConfigOperationsService _operationsService = operationsService;

    [HttpGet]
    [ProducesResponseType(typeof(ConfigSummary), StatusCodes.Status200OK)]
    public ActionResult<ConfigSummary> Get() => Ok(_operationsService.GetSummary());

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ConfigSummary), StatusCodes.Status200OK)]
    public ActionResult<ConfigSummary> Refresh() => Ok(_operationsService.RefreshSummary());

    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<ConfigUserSummary>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ConfigUserSummary>> GetUsers() =>
        Ok(_operationsService.GetUsers());

    [HttpGet("fetch")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, int>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyDictionary<string, int>> GetFetchCounts() =>
        Ok(_operationsService.GetFetchCounts());

    [HttpGet("stores")]
    [ProducesResponseType(typeof(ConfigStoresSummary), StatusCodes.Status200OK)]
    public ActionResult<ConfigStoresSummary> GetStores() => Ok(_operationsService.GetStores());
}
