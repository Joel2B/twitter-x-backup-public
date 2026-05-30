namespace Backup.Application.Network;

public interface IRequestQueryStringPolicyService
{
    string Build(
        string baseUrl,
        IReadOnlyDictionary<string, object?> variables,
        IReadOnlyDictionary<string, bool> features,
        IReadOnlyDictionary<string, bool> fieldToggles
    );
}
