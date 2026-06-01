namespace Backup.Application.Core;

public interface IDateTimeProvider
{
    DateTime Now { get; }
}
