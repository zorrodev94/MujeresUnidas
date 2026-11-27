using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Models.Publications
{
    /// <summary>Server-backed likes, bookmarks and comments on approved community publications.</summary>
    public sealed class PostInteraction(ApiClient api)
    {
        #region Operation Data
        private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        public readonly record struct CommentData(Guid Id,Guid AuthorId,string Author,
            string Body,DateTime CreatedUtc,bool IsMine);
        public readonly record struct SummaryData(int Likes,int Comments,bool Liked,bool Saved,
            IReadOnlyList<CommentData> RecentComments);
        #endregion Structs
        #region Task
        public async Task<(SummaryData? Value,string Error)> ReadAsync(Guid post)
        {
            var result=await api.RequestAsync(HttpMethod.Get,$"api/posts/{post}/engagement");
            if(!result.Success)return(null,result.Error);
            try{return(JsonSerializer.Deserialize<SummaryData>(result.Data.GetRawText(),JsonOptions),string.Empty);}
            catch(JsonException){return(null,"Formato de interacciones inválido.");}
        }
        public async Task<(bool Success,string Error)> LikeAsync(Guid post,bool value)
        {
            var result=await api.RequestAsync(value?HttpMethod.Put:HttpMethod.Delete,$"api/posts/{post}/like");
            return(result.Success,result.Error);
        }
        public async Task<(bool Success,string Error)> SaveAsync(Guid post,bool value)
        {
            var result=await api.RequestAsync(value?HttpMethod.Put:HttpMethod.Delete,$"api/posts/{post}/save");
            return(result.Success,result.Error);
        }
        public async Task<(bool Success,string Error)> CommentAsync(Guid post,string text)
        {
            var result=await api.RequestAsync(HttpMethod.Post,$"api/posts/{post}/comments",new{body=text});
            return(result.Success,result.Error);
        }
        public async Task<(bool Success,string Error)> RemoveCommentAsync(Guid post,Guid comment)
        {
            var result=await api.RequestAsync(HttpMethod.Delete,$"api/posts/{post}/comments/{comment}");
            return(result.Success,result.Error);
        }
        #endregion Task
    }
}
