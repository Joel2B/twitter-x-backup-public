namespace Backup.Application.Network;

public interface IRetryDelayPolicyService
{
    int GetDelayMilliseconds(int minSeconds, int maxSeconds);
}
