using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Gestion.Connection;
using MujeresUnidas.Shared.Contracts;

namespace MujeresUnidas.Gestion.Models.User.Base
{
    /// <summary>Defines shared authentication inputs and operations for institutional accounts.</summary>
    public abstract class User : IClientSession
    {
        #region Operation Data
        protected readonly ApiConnection Api;
        #endregion Operation Data
        #region Initialization
        protected User(ApiConnection api) { Api = api; }
        #endregion Initialization
        #region Arrays
        /// <summary>Default display options; the authoritative catalogue is read from Server.</summary>
        internal static readonly List<string> RoleUserGestion = new()
        {
            "Administradora", "Psicóloga", "Jurídica", "Trabajo social"
        };
        #endregion Arrays
        #region Structs
        public struct LoginData
        {
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; }
            [Required, StringLength(128)]
            public string Password { get; set; }
        }
        public readonly record struct OperationResultData(bool Success, string Message);
        /// <summary>Community badge assignment and its institutional approval state.
        /// Defined on User so all eligible linked community accounts share the model.
        /// No badge permissions are granted through this data alone.</summary>
        public readonly record struct UserBadgeData(
            Guid UserId,Guid BadgeId,Guid RequestedByUserId,DateTime RequestedUtc,
            Guid? ReviewedByUserId,DateTime? ReviewedUtc,string Status);
        /// <summary>Private account attributes used only in self-service editing.</summary>
        public readonly record struct PersonalDetailsData(string DisplayName,string? FirstName,
            string? PaternalLastName,string? MaternalLastName,string? Biography,
            string? PhoneNumber,string? AlternatePhoneNumber,DateTime? DateOfBirth,
            string? Address,string? City,string? State,string? PostalCode,string? Curp,string? Rfc);
        #endregion Structs
        #region Task
        public abstract Task<OperationResultData> LoginAsync(LoginData data);
        /// <summary>One session revocation API for every institutional account subtype.</summary>
        public virtual Task SignOutAsync()=>Api.SignOutAsync();
        #endregion Task
    }
}
