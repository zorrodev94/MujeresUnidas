using System.ComponentModel.DataAnnotations;

namespace MujeresUnidas.Server.Models.Messages
{
    /// <summary>A message has one author and one conversation, independently of participant count.</summary>
    public sealed class Message
    {
        #region Structs
        public readonly record struct MessageData(Guid MessageId, Guid ConversationId, Guid SenderUserId,
            string SenderName, string Body, DateTime CreatedUtc, DateTime? EditedUtc, bool IsDeleted);

        public struct SendData
        {
            [Required, StringLength(2000, MinimumLength = 1)] public string Body { get; set; }
        }
        public struct EditData
        {
            [Required, StringLength(2000, MinimumLength = 1)] public string Body { get; set; }
        }
        #endregion Structs
    }
}
