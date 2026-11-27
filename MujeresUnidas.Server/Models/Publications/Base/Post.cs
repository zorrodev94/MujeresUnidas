using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Models.Publications.Base
{
    /// <summary>Coordinates the community editorial lifecycle; the repository owns SQL persistence.</summary>
    public class Post
    {
        #region Operation Data
        protected readonly PostRepository Repository;
        /// <summary>Categories displayed on the Community Home. They are not institutional roles.</summary>
        internal static readonly List<string> Categories = new() { "Moment", "Blog", "Guide", "Job", "Event" };
        #endregion Operation Data
        #region Initialization
        public Post(PostRepository repository) { Repository = repository; }
        #endregion Initialization
        #region Structs
        /// <summary>Text metadata submitted first; authorized JPEG/PNG images are linked in a separate secure media workflow.</summary>
        public struct CreateData
        {
            [Required, StringLength(12)] public string Category { get; set; }
            [StringLength(48)] public string? Subcategory { get; set; }
            [StringLength(16)] public string? Mode { get; set; }
            [Required, StringLength(120, MinimumLength = 4)] public string Title { get; set; }
            [Required, StringLength(260, MinimumLength = 8)] public string Summary { get; set; }
            [Required, StringLength(12000, MinimumLength = 8)] public string Body { get; set; }
        }

        /// <summary>Editable author data; all edits are re-moderated for regular members.</summary>
        public struct EditData
        {
            [StringLength(48)] public string? Subcategory { get; set; }
            [StringLength(16)] public string? Mode { get; set; }
            [Required, StringLength(120, MinimumLength = 4)] public string Title { get; set; }
            [Required, StringLength(260, MinimumLength = 8)] public string Summary { get; set; }
            [Required, StringLength(12000, MinimumLength = 8)] public string Body { get; set; }
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
            bool Example, DateTime CreatedUtc, string? Subcategory = null, string? Mode = null);
        /// <summary>Result of polymorphic content and taxonomy validation.</summary>
        public readonly record struct ValidationData(bool Success,string Message);
        #endregion Structs
        #region Task
        /// <summary>Returns the real subtype that owns the category validation rules.</summary>
        public Post ForCategory(string category) => category switch
        {
            "Moment" => new Moment(Repository), "Blog" => new Blog(Repository), "Guide" => new Guide(Repository),
            "Job" => new EmploymentPost(Repository), "Event" => new Event(Repository),
            _ => throw new ArgumentOutOfRangeException(nameof(category),"Unknown post type.")
        };
        /// <summary>Overridden by each publication subtype to validate distinct business rules.</summary>
        public virtual ValidationData ValidateCreate(CreateData data) => new(false,"Unsupported post category.");
        protected static ValidationData ValidateCommon(CreateData data,int bodyLimit, IReadOnlyCollection<string> allowed)
        {
            if(string.IsNullOrWhiteSpace(data.Title)||data.Title.Trim().Length is <4 or >120 ||
               string.IsNullOrWhiteSpace(data.Summary)||data.Summary.Trim().Length is <8 or >260 ||
               string.IsNullOrWhiteSpace(data.Body)||data.Body.Trim().Length<20 ||
               data.Body.Trim().Length>bodyLimit)
                return new(false,$"Title 4-120, summary 8-260, body 20-{bodyLimit} characters.");
            if(string.IsNullOrWhiteSpace(data.Subcategory) ||
               !allowed.Contains(data.Subcategory,StringComparer.OrdinalIgnoreCase))
                return new(false,"Choose a valid subcategory for the publication type.");
            return new(true,string.Empty);
        }
        /// <summary>Validates author changes against the persisted category before saving.</summary>
        public async Task<ValidationData> ValidateEditAsync(Guid postId,EditData data)
        {
            string? category=await Repository.CategoryAsync(postId);
            if(category is null)return new(false,"Publication not found.");
            return ForCategory(category).ValidateCreate(new CreateData
            {Category=category,Subcategory=data.Subcategory,Mode=data.Mode,
                Title=data.Title,Summary=data.Summary,Body=data.Body});
        }

        /// <summary>Loads approved content for the Home, optionally restricted by category.</summary>
        public Task<IReadOnlyList<ViewData>> GetFeedAsync(string? category, bool featured)
            => Repository.GetFeedAsync(category, featured);

        public Task<IReadOnlyList<ViewData>> GetByAuthorAsync(Guid id)
            => Repository.GetByAuthorAsync(id);

        /// <summary>Loads a member's posts irrespective of pending or approved status.</summary>
        public Task<IReadOnlyList<ViewData>> GetMineAsync(Guid authorId)
            => Repository.GetMineAsync(authorId);

        /// <summary>Loads pending posts only for staff who passed API authorization.</summary>
        public Task<IReadOnlyList<ViewData>> GetPendingAsync()
            => Repository.GetPendingAsync();

        /// <summary>Only authenticated Community Staff blogs may auto-approve; institutional categories remain pending.</summary>
        public async Task<Guid> CreatePostAsync(Guid authorId, CreateData data, bool autoApprove)
        {
            var result=ForCategory(data.Category).ValidateCreate(data);
            if(!result.Success)throw new ValidationException(result.Message);
            return await Repository.InsertAsync(authorId,data,autoApprove);
        }

        /// <summary>Updates an authorized post and re-submits a member's changes to moderation.</summary>
        public Task<bool> EditPostAsync(Guid postId, Guid actorId, bool staff, EditData data)
            => Repository.UpdateAsync(postId, actorId, staff, data);

        /// <summary>Soft-removes a post while keeping its audit history.</summary>
        public Task<bool> DeletePostAsync(Guid postId, Guid actorId, bool staff)
            => Repository.DeleteAsync(postId, actorId, staff);

        /// <summary>Approves or rejects a pending publication; only Community Staff may call it.</summary>
        public Task<bool> ReviewPostAsync(Guid postId, Guid reviewerId, ReviewData data)
            => Repository.ReviewAsync(postId, reviewerId, data,false);
        public Task<bool> ReviewInstitutionalAsync(Guid postId,Guid reviewerId,ReviewData data)
            => Repository.ReviewAsync(postId,reviewerId,data,true);
        public Task<IReadOnlyList<ViewData>> GetInstitutionalPendingAsync()
            => Repository.GetInstitutionalPendingAsync();
        #endregion Task
    }
}
