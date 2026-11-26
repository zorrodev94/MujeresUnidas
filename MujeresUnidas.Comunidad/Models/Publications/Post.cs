using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Models.Publications
{
    /// <summary>Community publication operations; MAUI pages never build HTTP requests directly.</summary>
    public sealed class Post
    {
        #region Operation Data
        private readonly ApiClient _api;
        internal static readonly List<string> Categories = new() { "Blog", "Guide", "Job", "Event" };
        #endregion Operation Data

        #region Initialization
        public Post(ApiClient api) { _api = api; }
        #endregion Initialization

        #region Structs
        /// <summary>Required data for a new community publication.</summary>
        public struct CreateData
        {
            [Required] public string Category { get; set; }
            [Required, StringLength(120, MinimumLength=4)] public string Title { get; set; }
            [Required, StringLength(260, MinimumLength=8)] public string Summary { get; set; }
            [Required, StringLength(2500, MinimumLength=20)] public string Body { get; set; }
        }

        /// <summary>Editable publication information; its category does not change.</summary>
        public struct EditData
        {
            [Required, StringLength(120, MinimumLength=4)] public string Title { get; set; }
            [Required, StringLength(260, MinimumLength=8)] public string Summary { get; set; }
            [Required, StringLength(2500, MinimumLength=20)] public string Body { get; set; }
        }

        /// <summary>Publication returned by the authenticated community feed.</summary>
        public readonly record struct ViewData(Guid Id, string Category, string Title, string Summary,
            string Body, string Author, string Status, bool Featured, bool Example, DateTime CreatedUtc);

        /// <summary>Operation status returned to the page for user feedback.</summary>
        public readonly record struct ResultData(bool Success, string Message);
        #endregion Structs

        #region Task
        /// <summary>Loads featured or category-specific approved content from Server.</summary>
        public Task<(bool Success, IReadOnlyList<ViewData> Items, string Error)> FeedAsync(string? category, bool featured = false)
            => ReadAsync("api/posts?featured=" + (featured ? "true" : "false") +
                (category is null ? string.Empty : "&category=" + Uri.EscapeDataString(category)));

        /// <summary>Loads the account's own pending, approved or rejected entries.</summary>
        public Task<(bool Success, IReadOnlyList<ViewData> Items, string Error)> MineAsync()
            => ReadAsync("api/posts/mine");

        /// <summary>Loads the pending moderation queue. Server rejects non-Staff callers.</summary>
        public Task<(bool Success, IReadOnlyList<ViewData> Items, string Error)> PendingAsync()
            => ReadAsync("api/posts/pending");

        /// <summary>Creates a post and lets Server decide whether it needs moderation.</summary>
        public async Task<ResultData> CreatePostAsync(CreateData data)
        {
            var reply = await _api.RequestAsync(HttpMethod.Post, "api/posts", data);
            if (!reply.Success) return new(false, reply.Error);
            return new(true, Message(reply.Data,"Solicitud registrada."));
        }

        /// <summary>Modifies one authorized publication, re-queueing regular member edits.</summary>
        public async Task<ResultData> EditPostAsync(Guid id, EditData data)
        {
            var reply = await _api.RequestAsync(HttpMethod.Put, $"api/posts/{id}",data);
            return new(reply.Success,reply.Success?"Publicación actualizada.":reply.Error);
        }

        /// <summary>Withdraws a member-owned post or a Staff-moderated post.</summary>
        public async Task<ResultData> DeletePostAsync(Guid id)
        {
            var reply = await _api.RequestAsync(HttpMethod.Delete,$"api/posts/{id}");
            return new(reply.Success,reply.Success?"Publicación retirada.":reply.Error);
        }

        /// <summary>Reviews a pending post; Server verifies Staff authorization.</summary>
        public async Task<ResultData> ReviewPostAsync(Guid id, bool approved, bool featured = false)
        {
            var reply = await _api.RequestAsync(HttpMethod.Post,$"api/posts/{id}/review",new { approved,featured });
            return new(reply.Success,reply.Success?(approved?"Aprobada.":"Rechazada."):reply.Error);
        }

        private async Task<(bool Success, IReadOnlyList<ViewData> Items, string Error)> ReadAsync(string path)
        {
            var reply = await _api.RequestAsync(HttpMethod.Get,path);
            if (!reply.Success) return (false,Array.Empty<ViewData>(),reply.Error);
            try
            {
                var items = new List<ViewData>();
                foreach (var item in reply.Data.EnumerateArray())
                {
                    items.Add(new ViewData(item.GetProperty("id").GetGuid(),
                        Get(item,"category"), Get(item,"title"), Get(item,"summary"),
                        Get(item,"body"),Get(item,"author"),Get(item,"status"),
                        item.GetProperty("featured").GetBoolean(),item.GetProperty("example").GetBoolean(),
                        item.GetProperty("createdUtc").GetDateTime()));
                }
                return (true,items,string.Empty);
            }
            catch(Exception error) when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                return (false,Array.Empty<ViewData>(),"La respuesta de publicaciones está incompleta.");
            }
        }

        private static string Get(JsonElement element, string property) =>
            element.GetProperty(property).GetString() ?? string.Empty;
        private static string Message(JsonElement element, string fallback) =>
            element.ValueKind==JsonValueKind.Object && element.TryGetProperty("message",out var value) && value.ValueKind==JsonValueKind.String
                ? value.GetString() ?? fallback : fallback;
        #endregion Task
    }
}
