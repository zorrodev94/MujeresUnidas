using System.ComponentModel.DataAnnotations;

namespace MujeresUnidas.Server.Models.Messages
{
    /// <summary>A conversation owns participant IDs in its ConversationData. SQL persists the list as JSON.</summary>
    public sealed class Conversation
    {
        #region Structs
        public readonly record struct ConversationData(Guid ConversationId, string Title, string Type,
            Guid CreatedById, IReadOnlyList<Guid> UserIds, DateTime CreatedUtc);

        public struct CreateData
        {
            [Required, StringLength(100, MinimumLength = 2)] public string Title { get; set; }
            [Required, RegularExpression("^(Direct|Group|Public)$")] public string Type { get; set; }
            public List<Guid> UserIds { get; set; }
        }
        public readonly record struct AddParticipantData(Guid UserId);
        #endregion Structs
    }
}
