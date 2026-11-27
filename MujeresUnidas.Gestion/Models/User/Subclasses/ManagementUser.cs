using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MujeresUnidas.Gestion.Connection;
using BaseUser = global::MujeresUnidas.Gestion.Models.User.Base.User;

namespace MujeresUnidas.Gestion.Models.User.Subclasses
{
    /// <summary>Institutional login and dashboard operations for Gestión.</summary>
    public sealed class ManagementUser : BaseUser
    {
        #region Initialization
        public ManagementUser(ApiConnection api) : base(api) { }
        #endregion Initialization
        #region Structs
        /// <summary>Information required when the owner adds institutional personnel.</summary>
        public struct StaffRegistrationData
        {
            [Required, StringLength(100,MinimumLength=2)]
            public string DisplayName { get; set; }
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; }
            [Required, StringLength(128,MinimumLength=10)]
            public string Password { get; set; }
            [Required, StringLength(64)]
            public string Role { get; set; }
        }

        /// <summary>Most recent audit event returned by Server.</summary>
        public readonly record struct ActivityData(string Action,string Account);

        /// <summary>Authorized account counts and audit items for Gestión Home.</summary>
        public readonly record struct DashboardData(int PendingAccounts,int ActiveAccounts,
            int StaffAccounts,int BlockedAccounts,IReadOnlyList<ActivityData> RecentEvents);
        public struct ProfileData
        {
            [Required,StringLength(100,MinimumLength=2)] public string DisplayName {get;set;}
            [StringLength(300)] public string? Biography {get;set;}
        }
        #endregion Structs
        #region Task
        /// <summary>Authenticates the staff or owner through the institutional endpoint.</summary>
        public override async Task<OperationResultData> LoginAsync(LoginData data)
        {
            var reply=await Api.SendAsync(HttpMethod.Post,"api/auth/login?application=Management",data);
            if(!reply.Success) return new(false,reply.Error);
            try
            {
                var user=reply.Data.GetProperty("user");
                var kind=user.GetProperty("kind").GetString();
                if(kind is not ("Owner" or "Staff"))
                    return new(false,"Esta cuenta corresponde a Comunidad.");
                var token=reply.Data.GetProperty("token").GetString();
                if(string.IsNullOrWhiteSpace(token)) return new(false,"Server no devolvió una sesión válida.");
                Api.SetSession(token,user);
                return new(true,"Sesión iniciada.");
            }
            catch(Exception ex) when(ex is KeyNotFoundException or InvalidOperationException or JsonException)
            {return new(false,"Respuesta de autenticación incompleta.");}
        }

        public async Task<OperationResultData> UpdateProfileAsync(ProfileData data)
        {
            var result=await Api.SendAsync(HttpMethod.Put,"api/management/profile",data);
            if(result.Success){Api.UpdateUser(result.Data);return new(true,"Perfil actualizado.");}
            return new(false,result.Error);
        }

        /// <summary>Returns the current authorized management dashboard summary.</summary>
        public async Task<(bool Success,DashboardData Data,string Error)> GetSummaryAsync()
        {
            var reply=await Api.SendAsync(HttpMethod.Get,"api/management/summary");
            if(!reply.Success) return (false,default,reply.Error);
            try
            {
                var root=reply.Data;
                var events=new List<ActivityData>();
                foreach(var item in root.GetProperty("recentEvents").EnumerateArray())
                    events.Add(new ActivityData(item.GetProperty("action").GetString()??"",
                        item.GetProperty("account").GetString()??""));
                return (true,new DashboardData(root.GetProperty("pendingAccounts").GetInt32(),
                    root.GetProperty("activeAccounts").GetInt32(),
                    root.GetProperty("staffAccounts").GetInt32(),
                    root.GetProperty("blockedAccounts").GetInt32(),events),string.Empty);
            }
            catch(Exception ex) when(ex is KeyNotFoundException or InvalidOperationException or JsonException)
            {return (false,default,"Server devolvió un resumen incompleto.");}
        }
        #endregion Task
    }
}
