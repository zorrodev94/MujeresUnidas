using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Models.Publications
{
    /// <summary>Coordinates the community editorial lifecycle; the repository owns SQL persistence.</summary>
    public sealed class Post
    {
        #region Operation Data
        private readonly PostRepository _repository;
        /// <summary>Categories displayed on the Community Home. They are not institutional roles.</summary>
        internal static readonly List<string> Categories = new() { "Blog", "Guide", "Job", "Event" };
        #endregion Operation Data

        #region Initialization
        public Post(PostRepository repository) { _repository = repository; }
        #endregion Initialization

        #region Structs
        /// <summary>Text-only author request; image upload is intentionally deferred to a secure media workflow.</summary>
        public struct CreateData
        {
            [Required, StringLength(12)] public string Category { get; set; }
            [Required, StringLength(120, MinimumLength = 4)] public string Title { get; set; }
            [Required, StringLength(260, MinimumLength = 8)] public string Summary { get; set; }
            [Required, StringLength(2500, MinimumLength = 20)] public string Body { get; set; }
        }

        /// <summary>Editable author data; all edits are re-moderated for regular members.</summary>
        public struct EditData
        {
            [Required, StringLength(120, MinimumLength = 4)] public string Title { get; set; }
            [Required, StringLength(260, MinimumLength = 8)] public string Summary { get; set; }
            [Required, StringLength(2500, MinimumLength = 20)] public string Body { get; set; }
        }

        /// <summary>Explicit moderation decision by an approved Community Staff account.</summary>
        public struct ReviewData
        {
            [Required] public bool Approved { get; set; }
            public bool Featured { get; set; }
        }

        /// <summary>Read-only publication view, excluding confidential author identifiers and registration data.</summary>
        public readonly record struct ViewData(Guid Id, string Category, string Title,
            string Summary, string Body, string Author, string Status, bool Featured,
            bool Example, DateTime CreatedUtc);
        #endregion Structs

        #region Task
        /// <summary>Loads approved content for the Home, optionally restricted by category.</summary>
        public Task<IReadOnlyList<ViewData>> GetFeedAsync(string? category, bool featured)
            => _repository.GetFeedAsync(category, featured);

        /// <summary>Loads a member's posts irrespective of pending or approved status.</summary>
        public Task<IReadOnlyList<ViewData>> GetMineAsync(Guid authorId)
            => _repository.GetMineAsync(authorId);

        /// <summary>Loads pending posts only for staff who passed API authorization.</summary>
        public Task<IReadOnlyList<ViewData>> GetPendingAsync()
            => _repository.GetPendingAsync();

        /// <summary>Creates a pending post for a member, or an approved one for authenticated staff.</summary>
        public Task<Guid> CreatePostAsync(Guid authorId, CreateData data, bool staff)
            => _repository.InsertAsync(authorId, data, staff);

        /// <summary>Updates an authorized post and re-submits a member's changes to moderation.</summary>
        public Task<bool> EditPostAsync(Guid postId, Guid actorId, bool staff, EditData data)
            => _repository.UpdateAsync(postId, actorId, staff, data);

        /// <summary>Soft-removes a post while keeping its audit history.</summary>
        public Task<bool> DeletePostAsync(Guid postId, Guid actorId, bool staff)
            => _repository.DeleteAsync(postId, actorId, staff);

        /// <summary>Approves or rejects a pending publication; only Community Staff may call it.</summary>
        public Task<bool> ReviewPostAsync(Guid postId, Guid reviewerId, ReviewData data)
            => _repository.ReviewAsync(postId, reviewerId, data);
        #endregion Task
    }
}
