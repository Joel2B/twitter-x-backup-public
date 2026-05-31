namespace Backup.Application.Media.Filter;

public sealed class MediaErrorExclusionService(IMediaErrorFilterPolicyService policyService)
    : IMediaErrorExclusionService
{
    private readonly IMediaErrorFilterPolicyService _policyService = policyService;

    public IReadOnlySet<string> GetExcludedIds(IEnumerable<MediaErrorMessage> messages) =>
        messages
            .Where(message => _policyService.ShouldExclude(message.Message))
            .Select(message => message.Id)
            .ToHashSet();
}
