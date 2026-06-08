using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Backup.Api.Models;
using Backup.Api.Routing;
using Backup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Root + "/posts")]
[Consumes("application/json")]
[Produces("application/json")]
public class PostsController(IPostIngestionService postIngestionService) : ControllerBase
{
    private readonly IPostIngestionService _postIngestionService = postIngestionService;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<PostSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PostSummary>>> GetPosts(
        [FromQuery] PostListQuery request,
        [FromServices] PostQueryService queryService,
        CancellationToken cancellationToken
    ) => Ok(await queryService.GetPosts(request, cancellationToken));

    [HttpGet("{postId}")]
    [ProducesResponseType(typeof(PostDetail), StatusCodes.Status200OK)]
    public async Task<ActionResult<PostDetail>> GetPost(
        string postId,
        [FromServices] PostQueryService queryService,
        CancellationToken cancellationToken
    ) => Ok(await queryService.GetPost(postId, cancellationToken));

    /// <summary>
    /// Parses and stores posts from a raw GraphQL timeline response.
    /// </summary>
    /// <param name="userId">Target user id used for index/merge context.</param>
    /// <param name="origin">Origin key used for index/merge context.</param>
    /// <param name="rawRequest">Raw JSON response body from X GraphQL.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Ingestion counts plus next cursor when present.</returns>
    [HttpPost("raw")]
    [ProducesResponseType(typeof(PostIngestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PostIngestResult>> SaveRaw(
        [FromQuery]
        [Required(ErrorMessage = "Query param 'userId' is required.")]
        [RegularExpression(@".*\S.*", ErrorMessage = "Query param 'userId' is required.")]
            string userId,
        [FromQuery]
        [Required(ErrorMessage = "Query param 'origin' is required.")]
        [RegularExpression(@".*\S.*", ErrorMessage = "Query param 'origin' is required.")]
            string origin,
        [FromBody] JsonElement rawRequest,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (rawRequest.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            ModelState.AddModelError("rawRequest", "Request body cannot be empty.");
            return ValidationProblem(ModelState);
        }

        PostIngestResult result = await _postIngestionService.IngestRaw(
            userId,
            origin,
            rawRequest.GetRawText(),
            cancellationToken
        );

        return Ok(result);
    }

    /// <summary>
    /// Validates and stores posts that are already processed in the app schema.
    /// </summary>
    /// <param name="userId">Target user id used for index/merge context.</param>
    /// <param name="origin">Origin key used for index/merge context.</param>
    /// <param name="posts">Processed post list that matches extension upload schema.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Ingestion counts for received and saved posts.</returns>
    [HttpPost("processed")]
    [ProducesResponseType(typeof(PostIngestResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PostIngestResult>> SaveProcessed(
        [FromQuery]
        [Required(ErrorMessage = "Query param 'userId' is required.")]
        [RegularExpression(@".*\S.*", ErrorMessage = "Query param 'userId' is required.")]
            string userId,
        [FromQuery]
        [Required(ErrorMessage = "Query param 'origin' is required.")]
        [RegularExpression(@".*\S.*", ErrorMessage = "Query param 'origin' is required.")]
            string origin,
        [FromBody]
        [Required(ErrorMessage = "Body is required.")]
        [MinLength(1, ErrorMessage = "At least one processed post is required.")]
            List<ProcessedPostInput> posts,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        PostIngestResult result = await _postIngestionService.IngestProcessed(
            userId,
            origin,
            posts,
            cancellationToken
        );

        return Ok(result);
    }

    [HttpPost("download")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationResult>> Download(
        [FromBody] PostDownloadRequest request,
        [FromServices] PostOperationsService operationsService,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await operationsService.Download(request, cancellationToken));
    }

    [HttpPost("recovery")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OperationResult>> Recovery(
        [FromBody] PostRecoveryRequest request,
        [FromServices] PostOperationsService operationsService,
        CancellationToken cancellationToken
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await operationsService.Recovery(request, cancellationToken));
    }

    [HttpGet("parity")]
    [ProducesResponseType(typeof(PostStoreParityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PostStoreParityResponse>> GetParity(
        [FromServices] PostOperationsService operationsService,
        CancellationToken cancellationToken
    ) => Ok(await operationsService.GetParity(cancellationToken));

    [HttpGet("counts")]
    [ProducesResponseType(typeof(PostCountsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PostCountsResponse>> GetCounts(
        [FromServices] PostOperationsService operationsService
    ) => Ok(await operationsService.GetCounts());

    [HttpGet("stores")]
    [ProducesResponseType(typeof(IReadOnlyList<PostStoreSummary>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PostStoreSummary>> GetStores(
        [FromServices] PostOperationsService operationsService
    ) => Ok(operationsService.GetStores());

    [HttpPost("by-ids")]
    [ProducesResponseType(
        typeof(IReadOnlyList<Backup.Infrastructure.Posts.Models.Stored.Post>),
        StatusCodes.Status200OK
    )]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<
        ActionResult<IReadOnlyList<Backup.Infrastructure.Posts.Models.Stored.Post>>
    > GetByIds(
        [FromBody] PostIdsRequest request,
        [FromServices] PostOperationsService operationsService
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await operationsService.GetByIds(request));
    }

    [HttpGet("media-inputs")]
    [ProducesResponseType(
        typeof(IReadOnlyList<Backup.Infrastructure.Posts.Models.Stored.MediaInput>),
        StatusCodes.Status200OK
    )]
    public async Task<
        ActionResult<IReadOnlyList<Backup.Infrastructure.Posts.Models.Stored.MediaInput>>
    > GetMediaInputs([FromServices] PostOperationsService operationsService) =>
        Ok(await operationsService.GetMediaInputs());

    [HttpPost("save")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Save(
        [FromServices] PostOperationsService operationsService
    ) => Ok(await operationsService.Save());

    [HttpPost("prune")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Prune(
        [FromServices] PostOperationsService operationsService
    ) => Ok(await operationsService.Prune());

    [HttpPost("replication")]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationResult>> Replicate(
        [FromServices] PostOperationsService operationsService
    ) => Ok(await operationsService.Replicate());
}
