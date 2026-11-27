using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Comunidad.Connection;
using MujeresUnidas.Shared.Contracts;

namespace MujeresUnidas.Comunidad.Models.User.Base
{
    /// <summary>Defines shared account inputs and a typed login operation for the Community client.</summary>
    public abstract class User : IClientSession
    {
        #region Operation Data
        protected readonly ApiClient Api;
        #endregion Operation Data
        #region Initialization
        protected User(ApiClient api) { Api = api; }
        #endregion Initialization
        #region Arrays
        /// <summary>Reserved account categories; Staff registration is not enabled in Comunidad R1.</summary>
        internal static readonly List<string> RoleUserComunidad = new() { "Miembro", "Staff", "Profesional autorizado" };
        #endregion Arrays
        #region Structs
        /// <summary>Credentials entered into the Community login form.</summary>
        public struct LoginData
        {
            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; }

            [Required, StringLength(128)]
            public string Password { get; set; }
        }

        /// <summary>Result presented by the form after a request.</summary>
        public readonly record struct OperationResultData(bool Success, string Message);
        /// <summary>Community badge assignment and its institutional approval state.
        /// Defined on User so all eligible linked community accounts share the model.
        /// No badge permissions are granted through this data alone.</summary>
        public readonly record struct UserBadgeData(
            Guid UserId,Guid BadgeId,Guid RequestedByUserId,DateTime RequestedUtc,
            Guid? ReviewedByUserId,DateTime? ReviewedUtc,string Status);
        #endregion Structs
        #region Task
        public abstract Task<OperationResultData> LoginAsync(LoginData data);
        /// <summary>All Community account roles can revoke their server session.</summary>
        public virtual Task SignOutAsync()=>Api.SignOutAsync();
        #endregion Task
    }
}
