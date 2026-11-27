using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;

namespace MujeresUnidas.Comunidad.Models.User.Profiles
{
    public sealed class CommunityDirectory(ApiClient api)
    {
        #region Structs
        public readonly record struct ProfileData(Guid UserId,string DisplayName,string Biography,
            string CommunityRole,int Followers,int Following,bool IsFollowed,bool IsSelf,
            bool HasAvatar,bool HasCover);
        #endregion Structs
        #region Task
        public Task<(IReadOnlyList<ProfileData> Items,string Error)> SearchAsync(string term)
            => ListAsync("api/community/profiles?q="+Uri.EscapeDataString(term));
        public Task<(IReadOnlyList<ProfileData> Items,string Error)> FollowersAsync(Guid user)
            => ListAsync($"api/community/profiles/{user}/followers");
        public Task<(IReadOnlyList<ProfileData> Items,string Error)> FollowingAsync(Guid user)
            => ListAsync($"api/community/profiles/{user}/following");
        private async Task<(IReadOnlyList<ProfileData> Items,string Error)> ListAsync(string route)
        {
            var reply=await api.RequestAsync(HttpMethod.Get,route);
            if(!reply.Success)return(Array.Empty<ProfileData>(),reply.Error);
            try {return(JsonSerializer.Deserialize<List<ProfileData>>(reply.Data.GetRawText(),
                new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??new(),string.Empty);}
            catch(JsonException){return(Array.Empty<ProfileData>(),"No se pudo leer la lista de personas.");}
        }
        public async Task<(ProfileData? Profile,string Error)> GetAsync(Guid id)
        {
            var reply=await api.RequestAsync(HttpMethod.Get,$"api/community/profiles/{id}");
            if(!reply.Success)return(null,reply.Error);
            try{return(JsonSerializer.Deserialize<ProfileData>(reply.Data.GetRawText(),
                new JsonSerializerOptions{PropertyNameCaseInsensitive=true}),string.Empty);}
            catch(JsonException){return(null,"No se pudo leer el perfil.");}
        }
        public async Task<(bool Success,string Error)> FollowAsync(Guid id,bool follow)
        {
            var reply=await api.RequestAsync(follow?HttpMethod.Post:HttpMethod.Delete,$"api/community/profiles/{id}/follow");
            return(reply.Success,reply.Error);
        }
        public Task<(bool Success,string Error)> UploadAsync(string type,FileResult image)
            => api.UploadImageAsync($"api/community/profiles/me/{type}",image);
        public Task<byte[]?> ImageAsync(Guid id,string type)
            => api.DownloadImageAsync($"api/community/profiles/{id}/{type}");
        #endregion Task
    }
}
