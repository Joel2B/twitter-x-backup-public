namespace Backup.Application.Proxy.Models;

public enum ProxyFailureExecutionAction
{
    None,
    RotateProxy,
    ThrowPoolExhausted,
    ThrowStopProcess,
}
