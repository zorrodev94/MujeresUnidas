using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Models.Publications
{
    /// <summary>Gestión reviews only Guide, Job and Event requests. Staff reviews Blog inside Comunidad.</summary>
    public sealed class InstitutionalPost(ApiConnection api)
    {
        #region Structs
        public readonly record struct PostData(Guid Id,string Category,string Title,string Summary,
            string Body,string Author,string Status,bool Featured,bool Example,DateTime CreatedUtc);
        public readonly record struct ReviewData(bool Approved,bool Featured);
        #endregion Structs
        #region Task
        public async Task<(IReadOnlyList<PostData> Items,string Error)> PendingAsync()
        {
            var result=await api.SendAsync(HttpMethod.Get,"api/posts/institutional/pending");
            if(!result.Success)return(Array.Empty<PostData>(),result.Error);
            try{return(JsonSerializer.Deserialize<List<PostData>>(result.Data.GetRawText(),
                new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new(),string.Empty);}
            catch(JsonException){return(Array.Empty<PostData>(),"No fue posible interpretar la revisión institucional.");}
        }
        /// <summary>Lists private image identifiers only after Server verifies the moderator.</summary>
        public async Task<IReadOnlyList<Guid>> MediaIdsAsync(Guid id)
        {
            var result=await api.SendAsync(HttpMethod.Get,$"api/posts/{id}/media");
            if(!result.Success)return Array.Empty<Guid>();
            try{return JsonSerializer.Deserialize<List<Guid>>(result.Data.GetRawText())??new();}
            catch(JsonException){return Array.Empty<Guid>();}
        }
        public Task<byte[]?> ImageAsync(Guid postId,Guid mediaId)
            => api.DownloadImageAsync($"api/posts/{postId}/media/{mediaId}");
        public async Task<(bool Success,string Error)> ReviewAsync(Guid id,bool approved,bool featured)
        {
            var result=await api.SendAsync(HttpMethod.Post,$"api/posts/institutional/{id}/review",new ReviewData(approved,featured));
            return(result.Success,result.Error);
        }
        #endregion Task
    }
}
