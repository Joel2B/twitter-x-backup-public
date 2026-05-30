namespace Backup.Application.Dump;

public interface IDumpSessionNamingPolicyService
{
    string CreateCurrentSessionName(DateTime now);
}
