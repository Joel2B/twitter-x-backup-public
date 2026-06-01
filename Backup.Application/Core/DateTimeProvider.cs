namespace Backup.Application.Core;

public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime Now => DateTime.Now;
}
