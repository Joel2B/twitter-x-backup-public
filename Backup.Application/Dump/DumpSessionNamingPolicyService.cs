namespace Backup.Application.Dump;

public sealed class DumpSessionNamingPolicyService : IDumpSessionNamingPolicyService
{
    public string CreateCurrentSessionName(DateTime now) => now.ToString("yyyy.MM.dd-HH.mm.ss");
}
