namespace Backup.Application.Media.Maintenance.Models;

public enum MediaCacheRecheckMutationKind
{
    None = 0,
    Invalid = 1,
    SkipMissing = 2,
    Remove = 3,
    Update = 4,
}
