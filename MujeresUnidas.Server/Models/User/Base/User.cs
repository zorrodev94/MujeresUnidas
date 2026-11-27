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
            "Miembro", "Staff", "Profesional autorizado"
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
        /// <summary>Community badge assignment and its institutional approval state.
        /// Defined on User so all eligible linked community accounts share the model.
        /// No badge permissions are granted through this data alone.</summary>
        public readonly record struct UserBadgeData(
            Guid UserId,Guid BadgeId,Guid RequestedByUserId,DateTime RequestedUtc,
            Guid? ReviewedByUserId,DateTime? ReviewedUtc,string Status);
        /// <summary>Private editable identity fields. Only the authenticated account can read them.</summary>
        public struct PersonalDetailsData
        {
            [Required,StringLength(100,MinimumLength=2)] public string DisplayName {get;set;}
            [StringLength(300)] public string? Biography {get;set;}
            [StringLength(70)] public string? FirstName {get;set;}
            [StringLength(70)] public string? PaternalLastName {get;set;}
            [StringLength(70)] public string? MaternalLastName {get;set;}
            [StringLength(24)] public string? PhoneNumber {get;set;}
            [StringLength(24)] public string? AlternatePhoneNumber {get;set;}
            public DateTime? DateOfBirth {get;set;}
            [StringLength(250)] public string? Address {get;set;}
            [StringLength(90)] public string? City {get;set;}
            [StringLength(90)] public string? State {get;set;}
            [StringLength(12)] public string? PostalCode {get;set;}
            [StringLength(13)] public string? Rfc {get;set;}
            [StringLength(18)] public string? Curp {get;set;}
        }
        /// <summary>Authenticated password change; invalidates all previously issued sessions.</summary>
        public struct ChangePasswordData
        {
            [Required] public string CurrentPassword {get;set;}
            [Required,StringLength(128,MinimumLength=10)] public string NewPassword {get;set;}
        }
        #endregion Structs
        #region Task
        /// <summary>Authenticates an account using the application boundary of the derived class.</summary>
        public abstract Task<(AuthResponse? Response, string Status)> LoginAsync(LoginData data);
        #endregion Task
    }
}
