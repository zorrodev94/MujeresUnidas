using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Gestion.Connection;

namespace MujeresUnidas.Gestion.Models.User.Base
{
    /// <summary>Defines shared authentication inputs and operations for institutional accounts.</summary>
    public abstract class User
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
        #endregion Structs

        #region Task
        public abstract Task<OperationResultData> LoginAsync(LoginData data);
        #endregion Task
    }
}
