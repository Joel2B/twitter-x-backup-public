using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Backup.Api.Swagger;

public sealed class OperationsApiExamplesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        string? path = context.ApiDescription.RelativePath;
        string? method = context.ApiDescription.HttpMethod;

        if (path is null || method is null)
            return;

        switch ((method.ToUpperInvariant(), path))
        {
            case ("GET", "api/v1/backup/plan"):
                Apply(
                    operation,
                    summary: "Returns the resolved backup execution plan.",
                    description: "Useful to inspect which users, sources and phases will execute before triggering a run.",
                    okExample: BackupPlanExample()
                );
                break;
            case ("POST", "api/v1/backup/run"):
                Apply(
                    operation,
                    summary: "Runs the full backup pipeline.",
                    description: "Executes posts, recovery, bulk, media and final post store verification according to the current plan.",
                    okExample: OperationCompleted("backup-run", "completed")
                );
                break;
            case ("POST", "api/v1/backup/posts"):
                Apply(
                    operation,
                    summary: "Runs only the post source downloads from the backup plan.",
                    description: "Skips recovery, bulk and media phases.",
                    okExample: OperationCompleted("backup-posts", "completed", "users=2, sources=5")
                );
                break;
            case ("POST", "api/v1/backup/recovery"):
                Apply(
                    operation,
                    summary: "Runs only the recovery phase for planned users.",
                    description: "Triggers post recovery for users marked with recovery enabled in the current plan.",
                    okExample: OperationCompleted("backup-recovery", "completed", "users=2")
                );
                break;
            case ("POST", "api/v1/backup/bulk"):
                Apply(
                    operation,
                    summary: "Runs only the bulk phase from the backup plan.",
                    description: "Returns skipped when bulk is disabled in config or no eligible users exist.",
                    okExample: OperationCompleted("backup-bulk", "completed", "users=1")
                );
                break;
            case ("POST", "api/v1/backup/media"):
                Apply(
                    operation,
                    summary: "Runs only the media orchestration phase from the backup plan.",
                    description: "Returns skipped when media is disabled in config.",
                    okExample: OperationCompleted("backup-media", "completed")
                );
                break;
            case ("POST", "api/v1/backup/verify-post-stores"):
                Apply(
                    operation,
                    summary: "Verifies parity across enabled post stores.",
                    description: "Executes the same final post store verification used by the full backup run.",
                    okExample: OperationCompleted("backup-verify-post-stores", "completed")
                );
                break;
            case ("POST", "api/v1/posts/download"):
                Apply(
                    operation,
                    summary: "Downloads posts for a configured user/source pair.",
                    description: "Builds an ApiContext from config and runs the post download flow for the selected source.",
                    requestExample: ParseJson(
                        """
                        {
                          "userId": "44196397",
                          "sourceId": "tweets"
                        }
                        """
                    ),
                    okExample: OperationCompleted(
                        "post-download",
                        "completed",
                        "user=44196397, source=tweets"
                    ),
                    badRequestExample: ValidationProblem(
                        "sourceId",
                        "The SourceId field is required."
                    )
                );
                break;
            case ("POST", "api/v1/posts/recovery"):
                Apply(
                    operation,
                    summary: "Runs post recovery for one configured user.",
                    description: "Loads that user's configured APIs from the config snapshot and executes recovery.",
                    requestExample: ParseJson(
                        """
                        {
                          "userId": "44196397"
                        }
                        """
                    ),
                    okExample: OperationCompleted("post-recovery", "completed", "user=44196397"),
                    badRequestExample: ValidationProblem("userId", "The UserId field is required.")
                );
                break;
            case ("GET", "api/v1/posts/parity"):
                Apply(
                    operation,
                    summary: "Returns the current parity report across post stores.",
                    description: "The response includes per-store counts and mismatch statuses between stores.",
                    okExample: ParseJson(
                        """
                        {
                          "verifiedAt": "2026-06-07T21:35:14.0000000+00:00",
                          "storeCount": 3,
                          "snapshots": [
                            {
                              "label": "nas post json",
                              "posts": 125000,
                              "profiles": 640,
                              "hashtags": 9410,
                              "medias": 872000,
                              "mediaVariants": 240000,
                              "indexEntries": 180000,
                              "changes": 3200,
                              "changeFields": 5400,
                              "hashMeta": 125000
                            }
                          ],
                          "statuses": [
                            {
                              "primaryLabel": "nas post json",
                              "secondaryLabel": "nas post sqlite",
                              "isMismatch": false,
                              "diffsText": ""
                            }
                          ]
                        }
                        """
                    )
                );
                break;
            case ("GET", "api/v1/posts/counts"):
                Apply(
                    operation,
                    summary: "Returns primary post count plus per-store aggregate counts.",
                    description: "Use this to quickly compare row volumes across the enabled post stores.",
                    okExample: ParseJson(
                        """
                        {
                          "primaryCount": 125000,
                          "stores": [
                            {
                              "id": "nas post json",
                              "isDefault": true,
                              "posts": 125000,
                              "profiles": 640,
                              "hashtags": 9410,
                              "medias": 872000,
                              "mediaVariants": 240000,
                              "indexEntries": 180000,
                              "changes": 3200,
                              "changeFields": 5400,
                              "hashMeta": 125000
                            }
                          ]
                        }
                        """
                    )
                );
                break;
            case ("GET", "api/v1/posts/stores"):
                Apply(
                    operation,
                    summary: "Lists enabled post stores.",
                    description: "Shows the resolved store id, concrete implementation type and which store is default.",
                    okExample: ParseJson(
                        """
                        [
                          {
                            "id": "nas post json",
                            "isDefault": true,
                            "storeType": "LocalPostData"
                          },
                          {
                            "id": "nas post sqlite",
                            "isDefault": false,
                            "storeType": "SqlitePostData"
                          }
                        ]
                        """
                    )
                );
                break;
            case ("GET", "api/v1/posts"):
                Apply(
                    operation,
                    summary: "Returns paged post summaries with optional filters.",
                    description: "Use query filters to narrow the result set by profile, username, deleted state, media presence or text.",
                    okExample: ParseJson(
                        """
                        {
                          "page": 1,
                          "pageSize": 50,
                          "totalItems": 125000,
                          "totalPages": 2500,
                          "hasPrevious": false,
                          "hasNext": true,
                          "items": [
                            {
                              "id": "1926001234567890000",
                              "profileId": "44196397",
                              "userName": "elonmusk",
                              "displayName": "Elon Musk",
                              "description": "Sample stored post.",
                              "createdAt": "Sat May 24 00:10:12 +0000 2026",
                              "deleted": false,
                              "retweeted": false,
                              "favorited": false,
                              "bookmarked": false,
                              "mediaCount": 1,
                              "hashtagCount": 1
                            }
                          ]
                        }
                        """
                    )
                );
                SetParameterExample(operation, "page", 1, "1-based page number.");
                SetParameterExample(operation, "pageSize", 50, "Items per page. Range: 1-250.");
                SetParameterExample(operation, "profileId", "44196397", "Exact profile id.");
                SetParameterExample(operation, "userName", "elonmusk", "Exact X username.");
                SetParameterExample(operation, "deleted", false, "Filter deleted or active posts.");
                SetParameterExample(operation, "hasMedia", true, "Require posts with media.");
                SetParameterExample(
                    operation,
                    "textContains",
                    "bitcoin",
                    "Case-insensitive substring search in description."
                );
                SetParameterExample(
                    operation,
                    "sort",
                    1,
                    "Sort mode. 1=CreatedAtDesc, 2=CreatedAtAsc, 3=IdDesc, 4=IdAsc."
                );
                break;
            case ("GET", "api/v1/posts/{postId}"):
                Apply(
                    operation,
                    summary: "Returns full detail for a single stored post.",
                    description: "Includes full profile, hashtags and media/variant details for the requested post id.",
                    okExample: ParseJson(
                        """
                        {
                          "id": "1926001234567890000",
                          "profile": {
                            "id": "44196397",
                            "userName": "elonmusk",
                            "displayName": "Elon Musk",
                            "bannerUrl": "https://pbs.twimg.com/profile_banners/example",
                            "imageUrl": "https://pbs.twimg.com/profile_images/example.jpg",
                            "following": false,
                            "mediaCount": 1200
                          },
                          "description": "Sample stored post.",
                          "createdAt": "Sat May 24 00:10:12 +0000 2026",
                          "deleted": false,
                          "retweeted": false,
                          "favorited": false,
                          "bookmarked": false,
                          "hashtags": [ "test" ],
                          "medias": [
                            {
                              "id": "m1",
                              "url": "https://pbs.twimg.com/media/example.jpg",
                              "type": "photo",
                              "durationMilis": null,
                              "variants": []
                            }
                          ]
                        }
                        """
                    )
                );
                SetParameterExample(operation, "postId", "1926001234567890000", "Stored post id.");
                break;
            case ("POST", "api/v1/posts/by-ids"):
                Apply(
                    operation,
                    summary: "Returns stored post documents for the requested ids.",
                    description: "Reads from the primary post store and returns only the matching posts.",
                    requestExample: ParseJson(
                        """
                        {
                          "ids": [ "1926001234567890000", "1926001234567890001" ]
                        }
                        """
                    ),
                    okExample: ParseJson(
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
                            "description": "Sample stored post.",
                            "retweeted": false,
                            "favorited": false,
                            "bookmarked": false,
                            "createdAt": "Sat May 24 00:10:12 +0000 2026",
                            "hashtags": [ "test" ],
                            "medias": [],
                            "deleted": false,
                            "changes": [],
                            "index": {}
                          }
                        ]
                        """
                    ),
                    badRequestExample: ValidationProblem("ids", "The Ids field is required.")
                );
                break;
            case ("GET", "api/v1/posts/media-inputs"):
                Apply(
                    operation,
                    summary: "Returns the media projection input derived from posts.",
                    description: "Useful for debugging what the media orchestration layer will receive from post data.",
                    okExample: ParseJson(
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
                            "medias": [
                              {
                                "id": "1926001234567890000",
                                "url": "https://pbs.twimg.com/media/example.jpg",
                                "type": "photo",
                                "videoInfo": null
                              }
                            ],
                            "deleted": false
                          }
                        ]
                        """
                    )
                );
                break;
            case ("POST", "api/v1/posts/save"):
                Apply(
                    operation,
                    summary: "Persists the current primary post store state.",
                    description: "When multiple post stores are enabled, save also triggers replication to secondary stores.",
                    okExample: OperationCompleted("post-save", "completed")
                );
                break;
            case ("POST", "api/v1/posts/prune"):
                Apply(
                    operation,
                    summary: "Prunes post data in the primary store.",
                    description: "Applies the store-specific prune logic to the primary post store.",
                    okExample: OperationCompleted("post-prune", "completed")
                );
                break;
            case ("POST", "api/v1/posts/replication"):
                Apply(
                    operation,
                    summary: "Replicates post data from the default store to secondary stores.",
                    description: "Skips when only one post store is enabled. Uses the same replication service as normal save replication.",
                    okExample: OperationCompleted("post-replication", "completed", "stores=3")
                );
                break;
            case ("POST", "api/v1/media/run"):
                Apply(
                    operation,
                    summary: "Runs the full media orchestration pipeline.",
                    description: "Loads media inputs, processes downloads, runs storage maintenance, replication and backups.",
                    okExample: OperationCompleted("media-run", "completed")
                );
                break;
            case ("GET", "api/v1/media/storages"):
                Apply(
                    operation,
                    summary: "Lists resolved media storages and whether maintenance is configured.",
                    description: "The storage id values are the ones expected by the storage-specific media endpoints.",
                    okExample: ParseJson(
                        """
                        [
                          {
                            "storageId": "nas media",
                            "hasMaintenance": true
                          },
                          {
                            "storageId": "local media",
                            "hasMaintenance": false
                          }
                        ]
                        """
                    )
                );
                break;
            case ("GET", "api/v1/media/summary"):
                Apply(
                    operation,
                    summary: "Returns summary counts for the current media projection.",
                    description: "Builds the media projection and returns high-level counts for inputs, grouped downloads and files.",
                    okExample: ParseJson(
                        """
                        {
                          "filteredOnly": true,
                          "mediaInputCount": 125000,
                          "downloadCount": 843995,
                          "fileCount": 1274000,
                          "storageCount": 2,
                          "storages": [ "local media", "nas media" ]
                        }
                        """
                    )
                );
                SetParameterExample(
                    operation,
                    "filteredOnly",
                    true,
                    "True uses the filtered projection. False uses the full projection."
                );
                break;
            case ("GET", "api/v1/media/inputs"):
                Apply(
                    operation,
                    summary: "Returns paged media input rows derived from posts.",
                    description: "Useful for understanding which posts currently feed the media projection stage.",
                    okExample: ParseJson(
                        """
                        {
                          "page": 1,
                          "pageSize": 50,
                          "totalItems": 125000,
                          "totalPages": 2500,
                          "hasPrevious": false,
                          "hasNext": true,
                          "items": [
                            {
                              "postId": "1926001234567890000",
                              "profileId": "44196397",
                              "userName": "elonmusk",
                              "displayName": "Elon Musk",
                              "deleted": false,
                              "mediaCount": 2,
                              "mediaTypes": [ "photo", "video" ],
                              "hasProfileImage": true,
                              "hasBannerImage": true
                            }
                          ]
                        }
                        """
                    )
                );
                SetParameterExample(operation, "page", 1, "1-based page number.");
                SetParameterExample(operation, "pageSize", 50, "Items per page. Range: 1-250.");
                SetParameterExample(operation, "postId", "1926001234567890000", "Exact post id.");
                SetParameterExample(operation, "profileId", "44196397", "Exact profile id.");
                SetParameterExample(operation, "deleted", false, "Filter deleted or active rows.");
                break;
            case ("GET", "api/v1/media/downloads"):
                Apply(
                    operation,
                    summary: "Returns paged grouped media downloads.",
                    description: "Each item represents one post id with its projected media files and current storage state.",
                    okExample: ParseJson(
                        """
                        {
                          "page": 1,
                          "pageSize": 25,
                          "totalItems": 843995,
                          "totalPages": 33760,
                          "hasPrevious": false,
                          "hasNext": true,
                          "items": [
                            {
                              "postId": "1926001234567890000",
                              "profileId": "44196397",
                              "userName": "elonmusk",
                              "displayName": "Elon Musk",
                              "deleted": false,
                              "fileCount": 2,
                              "items": []
                            }
                          ]
                        }
                        """
                    )
                );
                SetParameterExample(
                    operation,
                    "filteredOnly",
                    true,
                    "True uses the filtered projection. False uses the full projection."
                );
                SetParameterExample(operation, "page", 1, "1-based page number.");
                SetParameterExample(operation, "pageSize", 25, "Items per page. Range: 1-250.");
                SetParameterExample(operation, "postId", "1926001234567890000", "Exact post id.");
                SetParameterExample(operation, "profileId", "44196397", "Exact profile id.");
                break;
            case ("GET", "api/v1/media/downloads/{downloadId}"):
                Apply(
                    operation,
                    summary: "Returns one grouped media download by id.",
                    description: "The id matches the post id used by the media projection output.",
                    okExample: ParseJson(
                        """
                        {
                          "postId": "1926001234567890000",
                          "profileId": "44196397",
                          "userName": "elonmusk",
                          "displayName": "Elon Musk",
                          "deleted": false,
                          "fileCount": 2,
                          "items": [
                            {
                              "postId": "1926001234567890000",
                              "profileId": "44196397",
                              "userName": "elonmusk",
                              "url": "https://pbs.twimg.com/media/example.jpg",
                              "path": "posts/photo/m1/orig/example.jpg",
                              "storages": [
                                {
                                  "storageId": "nas media",
                                  "exists": true,
                                  "partitionId": 4,
                                  "streamSize": 123456,
                                  "fileSize": 123456
                                }
                              ]
                            }
                          ]
                        }
                        """
                    )
                );
                SetParameterExample(
                    operation,
                    "downloadId",
                    "1926001234567890000",
                    "Grouped media download id."
                );
                SetParameterExample(
                    operation,
                    "filteredOnly",
                    true,
                    "True uses the filtered projection. False uses the full projection."
                );
                break;
            case ("GET", "api/v1/media/files"):
                Apply(
                    operation,
                    summary: "Returns paged flattened media files.",
                    description: "Use this when you want one row per file instead of grouped downloads.",
                    okExample: ParseJson(
                        """
                        {
                          "page": 1,
                          "pageSize": 50,
                          "totalItems": 1274000,
                          "totalPages": 25480,
                          "hasPrevious": false,
                          "hasNext": true,
                          "items": [
                            {
                              "postId": "1926001234567890000",
                              "profileId": "44196397",
                              "userName": "elonmusk",
                              "url": "https://pbs.twimg.com/media/example.jpg",
                              "path": "posts/photo/m1/orig/example.jpg",
                              "storages": [
                                {
                                  "storageId": "nas media",
                                  "exists": true,
                                  "partitionId": 4,
                                  "streamSize": 123456,
                                  "fileSize": 123456
                                }
                              ]
                            }
                          ]
                        }
                        """
                    )
                );
                SetParameterExample(
                    operation,
                    "filteredOnly",
                    true,
                    "True uses the filtered projection. False uses the full projection."
                );
                SetParameterExample(operation, "page", 1, "1-based page number.");
                SetParameterExample(operation, "pageSize", 50, "Items per page. Range: 1-250.");
                SetParameterExample(operation, "postId", "1926001234567890000", "Exact post id.");
                SetParameterExample(operation, "profileId", "44196397", "Exact profile id.");
                SetParameterExample(
                    operation,
                    "pathContains",
                    "video",
                    "Case-insensitive substring search in the stored relative path."
                );
                SetParameterExample(
                    operation,
                    "urlContains",
                    "pbs.twimg.com",
                    "Case-insensitive substring search in the source url."
                );
                break;
            case ("POST", "api/v1/media/storage/{storageId}/pipeline"):
                Apply(
                    operation,
                    summary: "Runs the storage-specific media pipeline for one storage.",
                    description: "This executes prune, data check, integrity check, download and replication for the selected storage.",
                    okExample: OperationCompleted(
                        "media-storage-pipeline",
                        "completed",
                        "storage=nas media, downloads=843995"
                    )
                );
                break;
            case ("POST", "api/v1/media/storage/{storageId}/prune"):
                Apply(
                    operation,
                    summary: "Runs the media prune step for one storage.",
                    description: "Requires maintenance to be configured for that storage id.",
                    okExample: OperationCompleted(
                        "media-storage-prune",
                        "completed",
                        "storage=nas media, downloads=843995"
                    )
                );
                break;
            case ("POST", "api/v1/media/storage/{storageId}/check-data"):
                Apply(
                    operation,
                    summary: "Runs the storage data check step for one storage.",
                    description: "Requires maintenance to be configured for that storage id.",
                    okExample: OperationCompleted(
                        "media-storage-check-data",
                        "completed",
                        "storage=nas media, downloads=843995"
                    )
                );
                break;
            case ("POST", "api/v1/media/storage/{storageId}/check-integrity"):
                Apply(
                    operation,
                    summary: "Runs the storage integrity check step for one storage.",
                    description: "Requires maintenance to be configured for that storage id.",
                    okExample: OperationCompleted(
                        "media-storage-check-integrity",
                        "completed",
                        "storage=nas media, downloads=843995"
                    )
                );
                break;
            case ("POST", "api/v1/media/storage/{storageId}/download"):
                Apply(
                    operation,
                    summary: "Runs the media download step for one storage.",
                    description: "Uses the processed and filtered media download set as input.",
                    okExample: OperationCompleted(
                        "media-storage-download",
                        "completed",
                        "storage=nas media, downloads=843995"
                    )
                );
                break;
            case ("POST", "api/v1/media/storage/{storageId}/replicate"):
                Apply(
                    operation,
                    summary: "Runs the media replication step from one storage.",
                    description: "Replicates from the selected storage to the remaining configured storages.",
                    okExample: OperationCompleted(
                        "media-storage-replicate",
                        "completed",
                        "storage=nas media, downloads=843995"
                    )
                );
                break;
            case ("POST", "api/v1/media/backups/run"):
                Apply(
                    operation,
                    summary: "Runs configured media backup strategies.",
                    description: "Builds the filtered media download set and then executes the configured backup strategies.",
                    okExample: OperationCompleted(
                        "media-backups-run",
                        "completed",
                        "downloads=843995"
                    )
                );
                break;
            case ("POST", "api/v1/bulk/run"):
                Apply(
                    operation,
                    summary: "Runs the full bulk pipeline for one configured user.",
                    description: "Requires at least one bulk data store and one bulk source data store.",
                    requestExample: ParseJson(
                        """
                        {
                          "userId": "44196397"
                        }
                        """
                    ),
                    okExample: OperationCompleted("bulk-run", "completed", "user=44196397"),
                    badRequestExample: ValidationProblem("userId", "The UserId field is required.")
                );
                break;
            case ("POST", "api/v1/bulk/import"):
                Apply(
                    operation,
                    summary: "Runs only the bulk import step for one user.",
                    description: "Loads that user's API config and executes the import runner.",
                    requestExample: ParseJson(
                        """
                        {
                          "userId": "44196397"
                        }
                        """
                    ),
                    okExample: OperationCompleted("bulk-import", "completed", "user=44196397"),
                    badRequestExample: ValidationProblem("userId", "The UserId field is required.")
                );
                break;
            case ("POST", "api/v1/bulk/verify"):
                Apply(
                    operation,
                    summary: "Runs only the bulk verification step.",
                    description: "Requires at least one configured bulk data store.",
                    okExample: OperationCompleted("bulk-verify", "completed")
                );
                break;
            case ("POST", "api/v1/bulk/phase1"):
                Apply(
                    operation,
                    summary: "Runs only bulk phase 1 for one user.",
                    description: "Loads that user's API config and executes the phase 1 runner.",
                    requestExample: ParseJson(
                        """
                        {
                          "userId": "44196397"
                        }
                        """
                    ),
                    okExample: OperationCompleted("bulk-phase1", "completed", "user=44196397"),
                    badRequestExample: ValidationProblem("userId", "The UserId field is required.")
                );
                break;
            case ("POST", "api/v1/bulk/phase2"):
                Apply(
                    operation,
                    summary: "Runs only bulk phase 2 for one user.",
                    description: "Loads that user's API config and executes the phase 2 runner.",
                    requestExample: ParseJson(
                        """
                        {
                          "userId": "44196397"
                        }
                        """
                    ),
                    okExample: OperationCompleted("bulk-phase2", "completed", "user=44196397"),
                    badRequestExample: ValidationProblem("userId", "The UserId field is required.")
                );
                break;
            case ("POST", "api/v1/bulk/phase2-reset"):
                Apply(
                    operation,
                    summary: "Resets the bulk phase 2 state.",
                    description: "Requires at least one configured bulk data store.",
                    okExample: OperationCompleted("bulk-phase2-reset", "completed")
                );
                break;
            case ("POST", "api/v1/bulk/prune"):
                Apply(
                    operation,
                    summary: "Prunes persisted bulk data.",
                    description: "Requires at least one configured bulk data store.",
                    okExample: OperationCompleted("bulk-prune", "completed")
                );
                break;
            case ("GET", "api/v1/config"):
                Apply(
                    operation,
                    summary: "Returns the current merged config snapshot.",
                    description: "Includes users, fetch counts, stores, partitions and feature toggles from the loaded config snapshot.",
                    okExample: ConfigSummaryExample()
                );
                break;
            case ("POST", "api/v1/config/refresh"):
                Apply(
                    operation,
                    summary: "Reloads config from disk and returns the refreshed snapshot.",
                    description: "Use this after changing config files and wanting the API process to reload them.",
                    okExample: ConfigSummaryExample()
                );
                break;
            case ("GET", "api/v1/config/users"):
                Apply(
                    operation,
                    summary: "Returns configured users and their API sources.",
                    description: "A compact view of the user/source configuration used by posts and bulk operations.",
                    okExample: ParseJson(
                        """
                        [
                          {
                            "userId": "44196397",
                            "sources": [
                              {
                                "sourceId": "tweets",
                                "apiId": "search",
                                "enabled": true
                              }
                            ]
                          }
                        ]
                        """
                    )
                );
                break;
            case ("GET", "api/v1/config/fetch"):
                Apply(
                    operation,
                    summary: "Returns configured fetch counts by source id.",
                    description: "This is the same fetch count map used when building ApiContext for downloads.",
                    okExample: ParseJson(
                        """
                        {
                          "tweets": 40,
                          "media": 80
                        }
                        """
                    )
                );
                break;
            case ("GET", "api/v1/config/stores"):
                Apply(
                    operation,
                    summary: "Returns configured stores and partitions grouped by store context.",
                    description: "This is useful when validating whether post, media, bulk and backup stores were loaded as expected.",
                    okExample: ParseJson(
                        """
                        {
                          "postStores": [
                            {
                              "id": "nas post json",
                              "type": "local",
                              "enabled": true,
                              "isDefault": true,
                              "partitions": [ 0 ]
                            }
                          ],
                          "dumpStores": [],
                          "bulkStores": [],
                          "mediaStores": [
                            {
                              "id": "nas media",
                              "type": "local",
                              "enabled": true,
                              "isDefault": true,
                              "partitions": [ 4 ]
                            }
                          ],
                          "backupStores": [],
                          "partitions": [
                            {
                              "id": 0,
                              "name": "nas L:",
                              "type": "nas",
                              "enabled": true,
                              "size": 2650000000000,
                              "usableSpace": 1400000000000,
                              "paths": [ "/mnt/l/apps/twitter-x-backup" ],
                              "tags": [ "primary" ]
                            }
                          ]
                        }
                        """
                    )
                );
                break;
            case ("GET", "api/v1/partitions"):
                Apply(
                    operation,
                    summary: "Returns all resolved partitions.",
                    description: "Each partition entry includes size, usable space, paths and optional tags.",
                    okExample: PartitionListExample()
                );
                break;
            case ("GET", "api/v1/partitions/cache"):
                Apply(
                    operation,
                    summary: "Returns partitions eligible for cache usage.",
                    description: "This reflects the partition selection policy currently active in the runtime.",
                    okExample: PartitionListExample()
                );
                break;
            case ("GET", "api/v1/partitions/primary"):
                Apply(
                    operation,
                    summary: "Returns the current primary partition.",
                    description: "This is the partition selected by the primary partition policy.",
                    okExample: PartitionItemExample()
                );
                break;
            case ("GET", "api/v1/partitions/heavy"):
                Apply(
                    operation,
                    summary: "Returns the current heavy partition.",
                    description: "This is the partition selected for heavier storage workloads.",
                    okExample: PartitionItemExample()
                );
                break;
        }
    }

    private static void Apply(
        OpenApiOperation operation,
        string summary,
        string description,
        JsonNode? requestExample = null,
        JsonNode? okExample = null,
        JsonNode? badRequestExample = null
    )
    {
        operation.Summary = summary;
        operation.Description = description;

        if (requestExample is not null)
            SetRequestBodyExample(operation, requestExample);

        if (okExample is not null)
            SetResponseExample(operation, "200", okExample);

        if (badRequestExample is not null)
            SetResponseExample(operation, "400", badRequestExample);
    }

    private static void SetRequestBodyExample(OpenApiOperation operation, JsonNode example)
    {
        if (operation.RequestBody?.Content is null)
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

        if (response?.Content is null)
            return;

        foreach (OpenApiMediaType mediaType in response.Content.Values)
            mediaType.Example = example;
    }

    private static void SetParameterExample(
        OpenApiOperation operation,
        string parameterName,
        object example,
        string? description = null
    )
    {
        if (operation.Parameters is null)
            return;

        OpenApiParameter? parameter = operation
            .Parameters.OfType<OpenApiParameter>()
            .FirstOrDefault(item =>
                string.Equals(item.Name, parameterName, StringComparison.OrdinalIgnoreCase)
            );

        if (parameter is null)
            return;

        parameter.Example = example switch
        {
            bool value => JsonValue.Create(value),
            int value => JsonValue.Create(value),
            long value => JsonValue.Create(value),
            _ => JsonValue.Create(example.ToString()),
        };

        if (!string.IsNullOrWhiteSpace(description))
            parameter.Description = description;
    }

    private static JsonNode OperationCompleted(
        string operation,
        string status,
        string? detail = null
    ) =>
        ParseJson(
            detail is null
                ? $$"""
                {
                  "operation": "{{operation}}",
                  "status": "{{status}}",
                  "detail": null
                }
                """
                : $$"""
                {
                  "operation": "{{operation}}",
                  "status": "{{status}}",
                  "detail": "{{detail}}"
                }
                """
        );

    private static JsonNode ValidationProblem(string field, string message) =>
        ParseJson(
            $$"""
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "One or more validation errors occurred.",
              "status": 400,
              "errors": {
                "{{field}}": [ "{{message}}" ]
              },
              "traceId": "00-9bb4b910499e4f8683158ad8db31bfab-a4b1ef1452f64d0f-00"
            }
            """
        );

    private static JsonNode BackupPlanExample() =>
        ParseJson(
            """
            {
              "generatedAt": "2026-06-07T21:35:14.0000000+00:00",
              "isBulkEnabled": true,
              "isMediaEnabled": true,
              "users": [
                {
                  "userId": "44196397",
                  "runRecovery": true,
                  "runBulk": true,
                  "api": {
                    "tweets": {
                      "id": "search",
                      "enabled": true,
                      "request": {
                        "url": "https://x.com/i/api/graphql/example/SearchTimeline",
                        "variables": {
                          "rawQuery": "from:elonmusk include:nativeretweets"
                        },
                        "features": {
                          "responsive_web_graphql_exclude_directive_enabled": true
                        },
                        "fieldToggles": {},
                        "headers": {
                          "authorization": "[REDACTED]",
                          "x-csrf-token": "[REDACTED]"
                        }
                      }
                    }
                  },
                  "sources": [
                    {
                      "sourceId": "tweets",
                      "apiId": "search",
                      "count": 40,
                      "request": {
                        "url": "https://x.com/i/api/graphql/example/SearchTimeline",
                        "variables": {
                          "rawQuery": "from:elonmusk include:nativeretweets"
                        },
                        "features": {
                          "responsive_web_graphql_exclude_directive_enabled": true
                        },
                        "fieldToggles": {},
                        "headers": {
                          "authorization": "[REDACTED]",
                          "x-csrf-token": "[REDACTED]"
                        }
                      }
                    }
                  ]
                }
              ]
            }
            """
        );

    private static JsonNode ConfigSummaryExample() =>
        ParseJson(
            """
            {
              "version": 18,
              "loadedAt": "2026-06-07T21:35:14.0000000+00:00",
              "users": [
                {
                  "userId": "44196397",
                  "sources": [
                    {
                      "sourceId": "tweets",
                      "apiId": "search",
                      "enabled": true
                    }
                  ]
                }
              ],
              "postStores": [
                {
                  "id": "nas post json",
                  "type": "local",
                  "enabled": true,
                  "isDefault": true,
                  "partitions": [ 0 ]
                }
              ],
              "dumpStores": [],
              "bulkStores": [],
              "mediaStores": [
                {
                  "id": "nas media",
                  "type": "local",
                  "enabled": true,
                  "isDefault": true,
                  "partitions": [ 4 ]
                }
              ],
              "backupStores": [],
              "partitions": [
                {
                  "id": 0,
                  "name": "nas L:",
                  "type": "nas",
                  "enabled": true,
                  "size": 2650000000000,
                  "usableSpace": 1400000000000,
                  "paths": [ "/mnt/l/apps/twitter-x-backup" ],
                  "tags": [ "primary" ]
                }
              ],
              "fetchCounts": {
                "tweets": 40
              },
              "bulkEnabled": true,
              "mediaEnabled": true
            }
            """
        );

    private static JsonNode PartitionListExample() =>
        ParseJson(
            """
            [
              {
                "id": 0,
                "name": "nas L:",
                "type": "nas",
                "enabled": true,
                "size": 2650000000000,
                "usableSpace": 1400000000000,
                "paths": [ "/mnt/l/apps/twitter-x-backup" ],
                "tags": [ "primary" ]
              },
              {
                "id": 4,
                "name": "nas I:",
                "type": "nas",
                "enabled": true,
                "size": 8460000000000,
                "usableSpace": 4100000000000,
                "paths": [ "/mnt/i" ],
                "tags": [ "media" ]
              }
            ]
            """
        );

    private static JsonNode PartitionItemExample() =>
        ParseJson(
            """
            {
              "id": 0,
              "name": "nas L:",
              "type": "nas",
              "enabled": true,
              "size": 2650000000000,
              "usableSpace": 1400000000000,
              "paths": [ "/mnt/l/apps/twitter-x-backup" ],
              "tags": [ "primary" ]
            }
            """
        );

    private static JsonNode ParseJson(string json) =>
        JsonNode.Parse(json)
        ?? throw new InvalidOperationException("Failed to parse Swagger example JSON.");
}
