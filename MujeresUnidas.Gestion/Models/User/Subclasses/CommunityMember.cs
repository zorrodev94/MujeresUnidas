using System.Text.Json;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Models.User.Subclasses
{
    /// <summary>Community-account administration adapter used only in the authorized Gestión client.</summary>
    public sealed class CommunityMember
    {
        #region Operation Data
        private readonly ApiConnection _api;
        #endregion Operation Data
        #region Initialization
        public CommunityMember(ApiConnection api) { _api=api; }
        #endregion Initialization
        #region Structs
        /// <summary>Account row used to review a Community request in Gestión.</summary>
        public readonly record struct AccountData(Guid Id,string DisplayName,string? Curp,
            string Email,string Status,string ActivationStatus,DateTime CreatedUtc,string CommunityRole,
            DateTime? ProposedVisitUtc,bool IdentityChecked,string? LocationLabel);

        /// <summary>Result of a community account activation or block operation.</summary>
        public readonly record struct OperationResultData(bool Success,string Message);
        /// <summary>Explicit role selected by the institutional reviewer, never by registration.</summary>
        public readonly record struct ActivationData(string CommunityRole);
        #endregion Structs
        #region Task
        /// <summary>Returns Community registrations, subject to Server permissions.</summary>
        public async Task<(bool Success,IReadOnlyList<AccountData> Accounts,string Error)> GetAccountsAsync()
        {
            var reply=await _api.SendAsync(HttpMethod.Get,"api/accounts");
            if(!reply.Success)return(false,Array.Empty<AccountData>(),reply.Error);
            try
            {
                var items=new List<AccountData>();
                foreach(var entry in reply.Data.EnumerateArray())
                    items.Add(new AccountData(entry.GetProperty("id").GetGuid(),
                        entry.GetProperty("displayName").GetString()??"",
                        entry.GetProperty("curp").ValueKind==JsonValueKind.String?entry.GetProperty("curp").GetString():null,
                        entry.GetProperty("email").GetString()??"",
                        entry.GetProperty("status").GetString()??"",
                        entry.GetProperty("activationStatus").GetString()??"",
                        entry.GetProperty("createdUtc").GetDateTime(),
                        entry.GetProperty("communityRole").GetString()??"Member",
                        entry.TryGetProperty("proposedVisitUtc",out var proposed)&&proposed.ValueKind==JsonValueKind.String
                            ?proposed.GetDateTime():null,
                        entry.TryGetProperty("identityChecked",out var checkedItem)&&checkedItem.ValueKind==JsonValueKind.True,
                        entry.TryGetProperty("locationLabel",out var place)&&place.ValueKind==JsonValueKind.String?place.GetString():null));
                return(true,items,string.Empty);
            }
            catch(Exception ex) when(ex is InvalidOperationException or KeyNotFoundException or JsonException or FormatException)
            {return(false,Array.Empty<AccountData>(),"Lista de Comunidad incompleta.");}
        }

        /// <summary>Approves the community activation; does not verify a real-world identity.</summary>
        public async Task<OperationResultData> ActivateAsync(Guid id,string communityRole)
        {
            var reply=await _api.SendAsync(HttpMethod.Post,$"api/accounts/{id}/activate",new ActivationData(communityRole));
            return new(reply.Success,reply.Success?"Cuenta activada.":reply.Error);
        }

        /// <summary>Blocks access to a Community account.</summary>
        public async Task<OperationResultData> BlockAsync(Guid id)
        {
            var reply=await _api.SendAsync(HttpMethod.Post,$"api/accounts/{id}/block");
            return new(reply.Success,reply.Success?"Cuenta bloqueada.":reply.Error);
        }
        #endregion Task
    }
}
