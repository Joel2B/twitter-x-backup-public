using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backup.App.Api.Swagger;

public class PostsApiExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        string? relativePath = context.ApiDescription.RelativePath;
        string? method = context.ApiDescription.HttpMethod;

        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(relativePath, "api/posts/processed", StringComparison.OrdinalIgnoreCase))
        {
            ApplyProcessedExamples(operation);
            return;
        }

        if (string.Equals(relativePath, "api/posts/raw", StringComparison.OrdinalIgnoreCase))
            ApplyRawExamples(operation);
    }

    private static void ApplyProcessedExamples(OpenApiOperation operation)
    {
        operation.Summary =
            "Accepts already-processed posts and saves them after model validation.";
        operation.Description =
            "Use this endpoint from the extension upload flow. Invalid payloads return a detailed ValidationProblem (400).";

        SetQueryExamples(operation);
        SetRequestBodyExample(operation, BuildProcessedRequestExample());
        SetResponseExample(operation, "200", BuildOkExample());
        SetResponseExample(operation, "400", BuildValidationProblemExample());
        SetResponseExample(
            operation,
            "500",
            BuildProblemExample("Processed post payload could not be saved.")
        );
    }

    private static void ApplyRawExamples(OpenApiOperation operation)
    {
        operation.Summary = "Accepts raw GraphQL response body and parses/saves posts.";
        operation.Description =
            "Use when you want the API to parse timeline entries directly from a raw X response.";

        SetQueryExamples(operation);
        SetRequestBodyExample(operation, BuildRawRequestExample());
        SetResponseExample(operation, "200", BuildOkExampleWithCursor());
        SetResponseExample(operation, "400", BuildBadRequestExample());
        SetResponseExample(
            operation,
            "500",
            BuildProblemExample("Raw post request could not be processed.")
        );
    }

    private static void SetQueryExamples(OpenApiOperation operation)
    {
        if (operation.Parameters is null)
            return;

        foreach (OpenApiParameter parameter in operation.Parameters)
        {
            if (string.Equals(parameter.Name, "userId", StringComparison.OrdinalIgnoreCase))
                parameter.Example = JsonValue.Create("44196397");

            if (string.Equals(parameter.Name, "origin", StringComparison.OrdinalIgnoreCase))
                parameter.Example = JsonValue.Create("extension-search-timeline");
        }
    }

    private static void SetRequestBodyExample(OpenApiOperation operation, JsonNode example)
    {
        if (operation.RequestBody is null)
            return;

        if (operation.RequestBody.Content is null)
            return;

        foreach (OpenApiMediaType mediaType in operation.RequestBody.Content.Values)
            mediaType.Example = example;
    }

    private static void SetResponseExample(
        OpenApiOperation operation,
        string statusCode,
        JsonNode example
    )
    {
        if (operation.Responses is null)
            return;

        if (!operation.Responses.TryGetValue(statusCode, out IOpenApiResponse? response))
            return;

        if (response is null || response.Content is null)
            return;

        foreach (OpenApiMediaType mediaType in response.Content.Values)
            mediaType.Example = example;
    }

    private static JsonNode BuildProcessedRequestExample() =>
        ParseJson(
            """
            [
              {
                "id": "1926001234567890000",
                "profile": {
                  "id": "44196397",
                  "userName": "elonmusk",
                  "name": "Elon Musk",
                  "imageUrl": "https://pbs.twimg.com/profile_images/example.jpg",
                  "following": false
                },
                "description": "Sample post from extension upload.",
                "retweeted": false,
                "favorited": false,
                "bookmarked": false,
                "createdAt": "Sat May 24 00:10:12 +0000 2026",
                "hashtags": [ "test" ],
                "medias": [
                  {
                    "id": "1926001234567890000",
                    "url": "https://pbs.twimg.com/media/example.jpg",
                    "type": "photo",
                    "videoInfo": {
                      "durationMilis": 18765,
                      "variants": [
                        {
                          "contentType": "video/mp4",
                          "bitrate": 832000,
                          "url": "https://video.twimg.com/example.mp4"
                        }
                      ]
                    }
                  }
                ],
                "deleted": false
              }
            ]
            """
        );

    private static JsonNode BuildRawRequestExample() =>
        ParseJson(
            """
            {
              "data": {
                "search_by_raw_query": {
                  "search_timeline": {
                    "timeline": {
                      "instructions": [
                        {
                          "type": "TimelineAddEntries",
                          "entries": []
                        }
                      ]
                    }
                  }
                }
              }
            }
            """
        );

    private static JsonNode BuildOkExample() =>
        ParseJson(
            """
            {
              "receivedPosts": 3,
              "savedPosts": 3,
              "nextCursor": null
            }
            """
        );

    private static JsonNode BuildOkExampleWithCursor() =>
        ParseJson(
            """
            {
              "receivedPosts": 20,
              "savedPosts": 20,
              "nextCursor": "DAABCgABGemExampleCursor"
            }
            """
        );

    private static JsonNode BuildBadRequestExample() =>
        ParseJson(
            """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {
                "userId": [ "Query param 'userId' is required." ]
              },
              "traceId": "00-6dcf6f8d53ca40f7b9d5191126b5786f-bf6e3f30256d943f-00"
            }
            """
        );

    private static JsonNode BuildValidationProblemExample() =>
        ParseJson(
            """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {
                "posts[0].profile.id": [ "profile.id is required." ],
                "posts[0].description": [ "description is required." ]
              },
              "traceId": "00-5ed2ef33724517b3d0fe8d52fd627ee8-97ed67f4775a7f37-00"
            }
            """
        );

    private static JsonNode BuildProblemExample(string detail) =>
        ParseJson(
            $$"""
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
              "title": "An error occurred while processing your request.",
              "status": 500,
              "detail": "{{detail}}",
              "traceId": "00-1ef2a6dff9679f617a3fefb6f85075e2-57d0ecf49f340f3b-00"
            }
            """
        );

    private static JsonNode ParseJson(string json) =>
        JsonNode.Parse(json)
        ?? throw new InvalidOperationException("Failed to parse Swagger example JSON.");
}
