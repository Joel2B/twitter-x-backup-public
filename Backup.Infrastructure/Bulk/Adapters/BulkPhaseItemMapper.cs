using Backup.Application.Bulk.Models;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Adapters;

internal static class BulkPhaseItemMapper
{
    public static BulkItem ToApplication(BulkData source) =>
        new()
        {
            UserName = source.User.Name,
            UserId = source.User.Id,
            UserStatus = ToApplication(source.User.Status),
            Total = source.Total,
            Cursor = source.Cursor,
            Phase1Order = source.Order.Phase1,
            Phase2Order = source.Order.Phase2,
        };

    public static void ApplyToInfrastructure(BulkItem source, BulkData target)
    {
        target.User.Name = source.UserName;
        target.User.Id = source.UserId;
        target.User.Status = ToInfrastructure(source.UserStatus);
        target.Total = source.Total;
        target.Cursor = source.Cursor;
        target.Order.Phase1 = source.Phase1Order;
        target.Order.Phase2 = source.Phase2Order;
    }

    private static BulkUserStatus ToApplication(StatusUser status) =>
        status switch
        {
            StatusUser.None => BulkUserStatus.None,
            StatusUser.Active => BulkUserStatus.Active,
            StatusUser.Inactive => BulkUserStatus.Inactive,
            _ => BulkUserStatus.None,
        };

    private static StatusUser ToInfrastructure(BulkUserStatus status) =>
        status switch
        {
            BulkUserStatus.None => StatusUser.None,
            BulkUserStatus.Active => StatusUser.Active,
            BulkUserStatus.Inactive => StatusUser.Inactive,
            _ => StatusUser.None,
        };
}
