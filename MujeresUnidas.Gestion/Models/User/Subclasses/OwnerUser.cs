using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Gestion.Models.User.Contracts;
using BaseUser = global::MujeresUnidas.Gestion.Models.User.Base.User;

namespace MujeresUnidas.Gestion.Models.User.Subclasses
{
    /// <summary>Initial owner setup and owner-restricted institutional registration operations.</summary>
    public sealed class OwnerUser : BaseUser, IAccountAccess<OwnerUser.SetupData>
    {
        #region Initialization
        public OwnerUser(ApiConnection api) : base(api) { }
        #endregion Initialization
        #region Structs
        public struct SetupData
        {
            [Required, StringLength(100,MinimumLength=2)]
            public string DisplayName { get; set; }
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; }
            [Required, StringLength(128,MinimumLength=10)]
            public string Password { get; set; }
            [Required, MinLength(24)]
            public string SetupKey { get; set; }
        }
        /// <summary>Role choice loaded from the database; Code is sent to Server.</summary>
        public readonly record struct RoleOption(string Code,string DisplayName)
        {
            public override string ToString()=>DisplayName;
        }
        /// <summary>New role name; authorization is always decided by Server.</summary>
        public struct RoleRegistrationData
        {
            [Required,StringLength(100,MinimumLength=3)]
            public string DisplayName { get; set; }
        }
        #endregion Structs
        #region Task
        /// <summary>Queries the Server setup state; no secret is transferred.</summary>
        public async Task<(bool Success,bool OwnerExists,string Error)> OwnerExistsAsync()
        {
            var reply=await Api.SendAsync(HttpMethod.Get,"api/setup/status");
            if(!reply.Success)return (false,false,reply.Error);
            try{return (true,reply.Data.GetProperty("ownerExists").GetBoolean(),string.Empty);}
            catch(Exception ex) when(ex is InvalidOperationException or KeyNotFoundException or JsonException)
            {return (false,false,"Server devolvió una respuesta de instalación incompleta.");}
        }

        /// <summary>Authenticates the Owner; ordinary staff cannot enter this operation.</summary>
        public override async Task<OperationResultData> LoginAsync(LoginData data)
        {
            var reply=await Api.SendAsync(HttpMethod.Post,"api/auth/login?application=Owner",data);
            if(!reply.Success)return new(false,reply.Error);
            try
            {
                var user=reply.Data.GetProperty("user");
                if(user.GetProperty("kind").GetString()!="Owner") return new(false,"No es una cuenta Owner.");
                var token=reply.Data.GetProperty("token").GetString();
                if(string.IsNullOrWhiteSpace(token))return new(false,"Server no devolvió una sesión válida.");
                Api.SetSession(token,user);
                return new(true,"Sesión Owner iniciada.");
            }
            catch(Exception ex) when(ex is InvalidOperationException or KeyNotFoundException or JsonException)
            {return new(false,"Respuesta de autenticación incompleta.");}
        }

        /// <summary>Creates the unique initial owner through Server.</summary>
        public async Task<OperationResultData> RegisterAsync(SetupData data)
        {
            var reply=await Api.SendAsync(HttpMethod.Post,"api/setup/owner",data);
            return new(reply.Success,reply.Success?"Autoridad principal creada.":reply.Error);
        }

        /// <summary>Approves a community registration as Owner; the API verifies permissions.</summary>
        public async Task<OperationResultData> AcceptAccountAsync(Guid communityAccountId,string communityRole)
        {
            var reply = await Api.SendAsync(HttpMethod.Post,$"api/accounts/{communityAccountId}/activate",
                new CommunityMember.ActivationData(communityRole));
            return new(reply.Success,reply.Success?"Cuenta comunitaria aprobada.":reply.Error);
        }

        /// <summary>Creates an institutional staff account; Server verifies owner privileges.</summary>
        public async Task<OperationResultData> CreateStaffAsync(ManagementUser.StaffRegistrationData data)
        {
            var reply=await Api.SendAsync(HttpMethod.Post,"api/staff",data);
            return new(reply.Success,reply.Success?"Cuenta institucional creada.":reply.Error);
        }

        /// <summary>Reads the live positions available for the staff ComboBox.</summary>
        public async Task<(bool Success,IReadOnlyList<RoleOption> Roles,string Error)> GetRolesAsync()
        {
            var reply=await Api.SendAsync(HttpMethod.Get,"api/management/roles");
            if(!reply.Success)return (false,Array.Empty<RoleOption>(),reply.Error);
            try
            {
                var items=new List<RoleOption>();
                foreach(var item in reply.Data.EnumerateArray())
                    items.Add(new RoleOption(item.GetProperty("code").GetString()??"",
                        item.GetProperty("displayName").GetString()??""));
                return (true,items,string.Empty);
            }
            catch(Exception ex) when(ex is InvalidOperationException or KeyNotFoundException or JsonException)
            {return(false,Array.Empty<RoleOption>(),"Respuesta del catálogo incompleta.");}
        }

        /// <summary>Adds a new position with no privileges other than baseline institutional access.</summary>
        public async Task<OperationResultData> AddRoleAsync(RoleRegistrationData data)
        {
            var reply=await Api.SendAsync(HttpMethod.Post,"api/management/roles",data);
            return new(reply.Success,reply.Success?"Puesto agregado al catálogo.":reply.Error);
        }
        #endregion Task
    }
}
