namespace MujeresUnidas.Server.Models.Messages.Management
{
    /// <summary>Data contracts for direct institutional messaging, separate from community messages.</summary>
    public sealed class ManagementConversation
    {
        #region Operation Data
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        public readonly record struct StaffData(Guid Id,string DisplayName,string? Role);
        public readonly record struct ConversationData(Guid Id,Guid OtherUserId,string OtherUserName,
            string? OtherUserRole,DateTime UpdatedUtc);
        public readonly record struct MessageData(Guid Id,Guid SenderId,string SenderName,
            string Body,DateTime CreatedUtc);
        public readonly record struct StartData(Guid RecipientId);
        public readonly record struct SendData(string? Body);
        #endregion Structs
        #region Task
        #endregion Task
    }
}
