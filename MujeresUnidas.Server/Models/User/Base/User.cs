using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Models.User.Base
{
    /// <summary>Defines shared account data and the common authentication operation.</summary>
    public abstract class User
    {
        #region Operation Data
        /// <summary>Server operations and persistence are delegated to the account service.</summary>
        protected readonly AccountService Accounts;
        #endregion Operation Data

        #region Initialization
        protected User(AccountService accounts)
        {
            Accounts = accounts;
        }
        #endregion Initialization

        #region Arrays
        /// <summary>Initial roles displayed by Gestión; the active catalogue comes from SQL Server.</summary>
        internal static readonly List<string> RoleUserGestion = new()
        {
            "Administradora", "Psicóloga", "Jurídica", "Trabajo social"
        };

        /// <summary>Account categories reserved for future community registration flows.</summary>
        internal static readonly List<string> RoleUserComunidad = new()
        {
            "Miembro", "Staff"
        };
        #endregion Arrays

        #region Structs
        /// <summary>Credentials shared by Comunidad and Gestión login operations.</summary>
        public struct LoginData
        {
            [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
            [EmailAddress(ErrorMessage = "Ingresa un correo electrónico válido.")]
            [StringLength(254)]
            public string Email { get; set; }

            [Required(ErrorMessage = "La contraseña es obligatoria.")]
            [StringLength(128)]
            public string Password { get; set; }
        }
        #endregion Structs

        #region Task
        /// <summary>Authenticates an account using the application boundary of the derived class.</summary>
        public abstract Task<(AuthResponse? Response, string Status)> LoginAsync(LoginData data);
        #endregion Task
    }
}
