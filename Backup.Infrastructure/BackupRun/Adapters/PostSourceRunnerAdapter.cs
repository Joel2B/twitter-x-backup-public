using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostSourceRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IPostService> postServices,
    ILogger<PostSourceRunnerAdapter> logger
) : IPostSourceRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostSourceRunnerAdapter> _logger = logger;

    public async Task Run(BackupRunSourceExecution execution)
    {
        ApiContext apiContext = new()
        {
            Id = execution.ApiId,
            Count = execution.Count,
            UserId = execution.UserId,
            Request = ToRequest(execution.Request),
        };

        _logger.LogInfo("source: {source}", apiContext.Id);

        await _stepExecutor.Run(
            _postServices.Select(service => new PostSourceStep(_logger, service, apiContext))
        );
    }

    private static Request ToRequest(BackupRunRequestExecution request) =>
        new()
        {
            Url = request.Url,
            Query = new Query
            {
                Variables = request.Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                Features = request.Features.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                FieldToggles = request.FieldToggles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            },
            Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };

    private sealed class PostSourceStep(
        ILogger<PostSourceRunnerAdapter> logger,
        IPostService service,
        Backup.Infrastructure.Models.Config.Api.ApiContext apiContext
    ) : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"post service: {service.GetType().Name}"))
                await service.Download(apiContext);
        }
    }
}
