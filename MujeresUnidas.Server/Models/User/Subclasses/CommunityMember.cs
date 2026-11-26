using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Contracts;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.User.Enums;
using BaseUser = global::MujeresUnidas.Server.Models.User.Base.User;

namespace MujeresUnidas.Server.Models.User.Subclasses
{
    /// <summary>Defines operation data for requests submitted from Comunidad.</summary>
    public sealed class CommunityMember : BaseUser, IAccountAccess<CommunityMember.RegistrationData>
    {
        #region Initialization
        public CommunityMember(AccountService accounts) : base(accounts) { }
        #endregion Initialization

        #region Structs
        /// <summary>Minimum fields on the Community registration page; creates a Pending account.</summary>
        public struct RegistrationData
        {
            [Required(ErrorMessage = "Escribe tu nombre.")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
            public string DisplayName { get; set; }

            [Required(ErrorMessage = "Escribe tu correo.")]
            [EmailAddress(ErrorMessage = "Escribe un correo válido.")]
            [StringLength(254)]
            public string Email { get; set; }

            [Required(ErrorMessage = "Ingresa una CURP ficticia para el prototipo.")]
            [RegularExpression("^[A-Z0-9]{18}$", ErrorMessage = "La CURP de prueba debe tener exactamente 18 caracteres alfanuméricos.")]
            public string Curp { get; set; }

            /// <summary>Requested community category; requires institutional approval.</summary>
            [Required, RegularExpression("^(Member|Staff)$")]
            public string CommunityRole { get; set; }

            [Required(ErrorMessage = "Escribe una contraseña.")]
            [StringLength(128, MinimumLength = 10, ErrorMessage = "La contraseña debe contener entre 10 y 128 caracteres.")]
            public string Password { get; set; }
        }

        /// <summary>Editable public profile fields; confidential account identifiers are not editable here.</summary>
        public struct ProfileData
        {
            [Required, StringLength(100, MinimumLength = 2)]
            public string DisplayName { get; set; }

            [StringLength(300)]
            public string? Biography { get; set; }
        }
        #endregion Structs

        #region Task
        /// <summary>Logs in through the Community account boundary.</summary>
        public override Task<(AuthResponse? Response, string Status)> LoginAsync(LoginData data)
            => Accounts.LoginAsync(data, "Community");

        /// <summary>Submits the registration request without automatically activating the account.</summary>
        public Task<string> RegisterAsync(RegistrationData data) => Accounts.RegisterAsync(data);

        /// <summary>Updates the public profile after authorization by Server.</summary>
        public Task<ApiUser?> UpdateProfileAsync(Guid accountId, ProfileData data)
            => Accounts.UpdateProfileAsync(accountId, data);
        /// <summary>Returns community registrations for an authorized administrative request.</summary>
        public Task<IReadOnlyList<CommunityAccountRow>> GetAccountsAsync()
            => Accounts.AllCommunityAsync();

        /// <summary>Changes a community account state after server-side authorization.</summary>
        public Task<bool> ChangeStatusAsync(Guid id, AccountStatus status, Guid actorId)
            => Accounts.ChangeStatusAsync(id, status, actorId);
        #endregion Task
    }
}
