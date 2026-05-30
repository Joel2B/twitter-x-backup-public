using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Backup.Api.Models;
using Backup.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Controllers;

[ApiController]
[Route("api/posts")]
[Consumes("application/json")]
[Produces("application/json")]
public class PostsController(IPostIngestionService postIngestionService) : ControllerBase
{
    private readonly IPostIngestionService _postIngestionService = postIngestionService;

    /// <summary>
    /// Parses and stores posts from a raw GraphQL timeline response.
    /// </summary>
    /// <param name="userId">Target user id used for index/merge context.</param>
    /// <param name="origin">Origin key used for index/merge context.</param>
    /// <param name="rawRequest">Raw JSON response body from X GraphQL.</param>
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
        [FromBody] JsonElement rawRequest
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
            rawRequest.GetRawText()
        );

        return Ok(result);
    }

    /// <summary>
    /// Validates and stores posts that are already processed in the app schema.
    /// </summary>
    /// <param name="userId">Target user id used for index/merge context.</param>
    /// <param name="origin">Origin key used for index/merge context.</param>
    /// <param name="posts">Processed post list that matches extension upload schema.</param>
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
            List<ProcessedPostInput> posts
    )
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        PostIngestResult result = await _postIngestionService.IngestProcessed(
            userId,
            origin,
            posts
        );

        return Ok(result);
    }
}
