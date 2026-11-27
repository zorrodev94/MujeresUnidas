namespace MujeresUnidas.Server.Models.Notifications
{
    public sealed class Notification
    {
        #region Structs
        public readonly record struct NotificationData(Guid NotificationId,string Type,string Title,
            Guid? RelatedId,DateTime CreatedUtc,bool IsRead);
        #endregion Structs
    }
}
