using Backup.Application.BackupRun.Models;
using Backup.Application.BackupRun.Ports;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Config.ApiRequest;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.BackupRun.Adapters;

public class PostRecoveryRunnerAdapter(
    IEnumerable<IPostService> postServices,
    ILogger<PostRecoveryRunnerAdapter> logger
) : IPostRecoveryRunner
{
    private readonly IEnumerable<IPostService> _postServices = postServices;
    private readonly ILogger<PostRecoveryRunnerAdapter> _logger = logger;

    public async Task Run(BackupRunUserPlan user)
    {
        UsersContext userContext = new()
        {
            UserId = user.UserId,
            Api = user.Api.ToDictionary(
                kvp => kvp.Key,
                kvp => new ApiConfig
                {
                    Id = kvp.Value.Id,
                    Enabled = kvp.Value.Enabled,
                    Request = new Request
                    {
                        Url = kvp.Value.Request.Url,
                        Query = new Query
                        {
                            Variables = kvp
                                .Value
                                .Request
                                .Variables
                                .ToDictionary(entry => entry.Key, entry => entry.Value),
                            Features = kvp
                                .Value
                                .Request
                                .Features
                                .ToDictionary(entry => entry.Key, entry => entry.Value),
                            FieldToggles = kvp
                                .Value
                                .Request
                                .FieldToggles
                                .ToDictionary(entry => entry.Key, entry => entry.Value),
                        },
                        Headers = kvp
                            .Value
                            .Request
                            .Headers
                            .ToDictionary(entry => entry.Key, entry => entry.Value),
                    },
                }
            ),
        };

        foreach (IPostService service in _postServices)
        {
            using (_logger.LogTimer($"post recovery service: {service.GetType().Name}"))
                await service.Recover(userContext);
        }
    }
}
