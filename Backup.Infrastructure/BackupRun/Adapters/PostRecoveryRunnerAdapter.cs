using Backup.Application.BackupRun;
using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Posts.Abstractions.Services;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.Request;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostRecoveryRunnerAdapter(
    IBackupRunStepExecutor stepExecutor,
    IEnumerable<IPostService> postServices,
    ILogger<PostRecoveryRunnerAdapter> logger
) : IPostRecoveryRunner
{
    private readonly IBackupRunStepExecutor _stepExecutor = stepExecutor;
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostRecoveryRunnerAdapter> _logger = logger;

    public async Task Run(BackupRunRecoveryExecution execution)
    {
        UsersContext userContext = new()
        {
            UserId = execution.UserId,
            Api = execution.Api.ToDictionary(kvp => kvp.Key, kvp => ToApiConfig(kvp.Value)),
        };

        await _stepExecutor.Run(
            _postServices.Select(service => new PostRecoveryStep(_logger, service, userContext))
        );
    }

    private static ApiConfig ToApiConfig(BackupRunApiExecution api) =>
        new()
        {
            Id = api.Id,
            Enabled = api.Enabled,
            Request = ToRequest(api.Request),
        };

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

    private sealed class PostRecoveryStep(
        ILogger<PostRecoveryRunnerAdapter> logger,
        IPostService service,
        Backup.Infrastructure.Models.Config.Api.UsersContext userContext
    ) : IBackupRunStep
    {
        public async Task Run()
        {
            using (logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(userContext);
        }
    }
}
