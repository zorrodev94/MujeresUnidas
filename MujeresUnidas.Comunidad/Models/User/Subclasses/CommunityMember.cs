using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Comunidad.Models.User.Contracts;
using BaseUser = global::MujeresUnidas.Comunidad.Models.User.Base.User;

namespace MujeresUnidas.Comunidad.Models.User.Subclasses
{
    /// <summary>Community account operations; the forms do not perform HTTP requests.</summary>
    public sealed class CommunityMember : BaseUser, IAccountAccess<CommunityMember.RegistrationData>
    {
        #region Initialization
        public CommunityMember(ApiClient api) : base(api) { }
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

            /// <summary>Requested community category; requires institutional approval.</summary>
            [Required, RegularExpression("^(Member|Staff)$")]
            public string CommunityRole { get; set; }
            [Required, StringLength(128, MinimumLength = 10)]
            public string Password { get; set; }
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
            return new(reply.Success,reply.Success?"Solicitud enviada. Espera su activación desde Gestión.":reply.Error);
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

        /// <summary>Clears the local session when the user closes her account session.</summary>
        public async Task SignOutAsync()
        {
            try { await Api.RequestAsync(HttpMethod.Post,"api/auth/logout"); }
            finally { Api.SignOut(); }
        }
        public void SignOut() => Api.SignOut();
        #endregion Task
    }
}
