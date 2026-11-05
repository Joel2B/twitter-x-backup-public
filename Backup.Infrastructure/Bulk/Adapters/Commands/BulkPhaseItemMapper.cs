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
            VideoCursor = source.VideoCursor,
            VideoCompleted = source.VideoCompleted,
            PhotoCursor = source.PhotoCursor,
            PhotoCompleted = source.PhotoCompleted,
            Phase2VideoCursor = source.Phase2VideoCursor,
            Phase2VideoCompleted = source.Phase2VideoCompleted,
            Phase2PhotoCursor = source.Phase2PhotoCursor,
            Phase2PhotoCompleted = source.Phase2PhotoCompleted,
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
        target.VideoCursor = source.VideoCursor;
        target.VideoCompleted = source.VideoCompleted;
        target.PhotoCursor = source.PhotoCursor;
        target.PhotoCompleted = source.PhotoCompleted;
        target.Phase2VideoCursor = source.Phase2VideoCursor;
        target.Phase2VideoCompleted = source.Phase2VideoCompleted;
        target.Phase2PhotoCursor = source.Phase2PhotoCursor;
        target.Phase2PhotoCompleted = source.Phase2PhotoCompleted;
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
