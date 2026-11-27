using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Contracts;
using MujeresUnidas.Comunidad.Models.Publications;
using MujeresUnidas.Comunidad.Models.Messages;
using MujeresUnidas.Comunidad.Models.User.Profiles;
using BaseUser = global::MujeresUnidas.Comunidad.Models.User.Base.User;

namespace MujeresUnidas.Comunidad.Models.User.Subclasses
{
    /// <summary>Community account operations; the forms do not perform HTTP requests.</summary>
    public class CommunityMember : BaseUser, IAccountAccess<CommunityMember.RegistrationData>,
        IPublicationPublisher, IMessageParticipant, IFollowableProfile
    {
        #region Initialization
        public CommunityMember(ApiClient api) : base(api) { }
        public virtual string PublicRoleLabel=>"Miembro de Comunidad";
        public virtual bool CanReviewBlogs=>false;
        /// <summary>A UI projection only: no permissions are ever granted by the client class.</summary>
        public static CommunityMember FromSession(ApiClient api)
        {
            var user=api.CurrentUser;
            if(user.ValueKind!=JsonValueKind.Object || !user.TryGetProperty("communityRole",out var role))
                return new CommunityMember(api);
            return role.GetString() switch
            {
                "Staff"=>new CommunityStaff(api),
                "Professional"=>new CommunityProfessional(api),
                _=>new CommunityMember(api)
            };
        }
        #endregion Initialization
        #region Structs
        /// <summary>Data requested from a Community registration.</summary>
        public struct RegistrationData
        {
            [Required, StringLength(100, MinimumLength = 2)]
            public string DisplayName { get; set; }
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; }
            [Required, RegularExpression("^[A-Z0-9]{18}$")]
            public string Curp { get; set; }
            [Required, StringLength(128, MinimumLength = 10)]
            public string Password { get; set; }

            /// <summary>Voluntary self-declaration of eligibility; no gender inferred from names or documents.</summary>
            public bool EligibilityAffirmed { get; set; }
        }

        /// <summary>Editable public profile information.</summary>
        public struct ProfileData
        {
            [Required, StringLength(100, MinimumLength = 2)]
            public string DisplayName { get; set; }
            [StringLength(300)]
            public string? Biography { get; set; }
        }
        #endregion Structs
        #region Task
        /// <summary>Authenticates and stores the returned session for the Community client.</summary>
        public override async Task<OperationResultData> LoginAsync(LoginData data)
        {
            var reply = await Api.RequestAsync(HttpMethod.Post,"api/auth/login?application=Community",data);
            if(!reply.Success) return new(false,reply.Error);
            try
            {
                var user = reply.Data.GetProperty("user");
                if(user.GetProperty("kind").GetString() != "Community")
                    return new(false,"Esta cuenta pertenece a Gestión.");
                var token = reply.Data.GetProperty("token").GetString();
                if(string.IsNullOrWhiteSpace(token)) return new(false,"La respuesta no contiene una sesión válida.");
                Api.SignIn(token,user);
                return new(true,"Sesión iniciada.");
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or JsonException)
            { return new(false,"Server devolvió una respuesta de acceso incompleta."); }
        }

        /// <summary>Registers a Pending Community account without activating it.</summary>
        public async Task<OperationResultData> RegisterAsync(RegistrationData data)
        {
            var reply=await Api.RequestAsync(HttpMethod.Post,"api/auth/register",data);
            return new(reply.Success,reply.Success&&reply.Data.ValueKind==JsonValueKind.Object &&
                reply.Data.TryGetProperty("message",out var notice)?notice.GetString()??"Solicitud recibida.":reply.Error);
        }

        /// <summary>Reads the current authenticated public profile.</summary>
        public async Task<(bool Success,ProfileData Data,string Error)> GetProfileAsync()
        {
            var reply=await Api.RequestAsync(HttpMethod.Get,"api/auth/me");
            if(!reply.Success) return (false,default,reply.Error);
            try
            {
                var user=reply.Data;
                var data=new ProfileData
                {
                    DisplayName=user.GetProperty("displayName").GetString()??string.Empty,
                    Biography=user.TryGetProperty("biography",out var bio)&&bio.ValueKind==JsonValueKind.String
                        ?bio.GetString():string.Empty
                };
                Api.UpdateUser(user);
                return (true,data,string.Empty);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or JsonException)
            {return (false,default,"Server devolvió un perfil incompleto.");}
        }

        /// <summary>Persists editable public profile information through Server.</summary>
        public async Task<OperationResultData> UpdateProfileAsync(ProfileData data)
        {
            var reply=await Api.RequestAsync(HttpMethod.Put,"api/profile",data);
            if(reply.Success) Api.UpdateUser(reply.Data);
            return new(reply.Success,reply.Success?"Cambios guardados en SQL Server.":reply.Error);
        }

        /// <summary>Submits a typed publication; approval is decided by Server.</summary>
        public Task<Post.ResultData> CreatePostAsync(Post.CreateData data)
            => new Post(Api).CreatePostAsync(data);
        public Task<Post.ResultData> EditPostAsync(Guid id,Post.EditData data)
            => new Post(Api).EditPostAsync(id,data);
        public Task<Post.ResultData> DeletePostAsync(Guid id)
            => new Post(Api).DeletePostAsync(id);
        /// <summary>Starts a direct, group, or public conversation according to Server policy.</summary>
        public Task<(Guid? Id,string Error)> CreateConversationAsync(Conversation.CreateData data)
            => new Conversation(Api).CreateConversationAsync(data);
        /// <summary>Routes message operations through the dedicated model, never directly through a page.</summary>
        public Task<(bool Success,string Error)> SendMessageAsync(Guid conversationId,string body)
            => new Message(Api).SendMessageAsync(conversationId,body);
        public Task<(bool Success,string Error)> EditMessageAsync(Guid messageId,string body)
            => new Message(Api).EditMessageAsync(messageId,body);
        public Task<(bool Success,string Error)> DeleteMessageAsync(Guid messageId)
            => new Message(Api).DeleteMessageAsync(messageId);
        public Task<(bool Success,string Message)> LeaveConversationAsync(Guid conversationId)
            => new Conversation(Api).LeaveAsync(conversationId);
        /// <summary>Follows or unfollows a public Community identity with Server checks.</summary>
        public async Task<(bool Success,string Error)> FollowUserAsync(Guid userId)
        {
            var result=await new CommunityDirectory(Api).FollowAsync(userId,true);
            return (result.Success,result.Error);
        }
        public async Task<(bool Success,string Error)> UnfollowUserAsync(Guid userId)
        {
            var result=await new CommunityDirectory(Api).FollowAsync(userId,false);
            return (result.Success,result.Error);
        }
        /// <summary>Clears the local session when the user closes her account session.</summary>
        public override Task SignOutAsync() => base.SignOutAsync();
        public void SignOut() => Api.SignOut();
        #endregion Task
    }
}
