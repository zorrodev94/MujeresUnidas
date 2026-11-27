using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Services;
using BaseUser = global::MujeresUnidas.Server.Models.User.Base.User;

namespace MujeresUnidas.Server.Models.User.Subclasses
{
    /// <summary>Base for institutional accounts; community registrations never create these accounts.</summary>
    public class ManagementUser : BaseUser
    {
        #region Initialization
        public ManagementUser(AccountService accounts) : base(accounts) { }
        #endregion Initialization
        #region Structs
        /// <summary>Data entered by the Owner in Gestión &gt; Altas de personal.</summary>
        public struct StaffRegistrationData
        {
            [Required, StringLength(100, MinimumLength = 2)]
            public string DisplayName { get; set; }

            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; }

            [Required, StringLength(128, MinimumLength = 10)]
            public string Password { get; set; }

            [Required, StringLength(64)]
            public string Role { get; set; }
        }
        /// <summary>Editable institutional presentation; roles and permissions are not editable here.</summary>
        public struct ProfileData
        {
            [Required,StringLength(100,MinimumLength=2)] public string DisplayName { get; set; }
            [StringLength(300)] public string? Biography { get; set; }
        }
        #endregion Structs
        #region Task
        /// <summary>Authenticates an institutional account, including owner access.</summary>
        public override Task<(AuthResponse? Response, string Status)> LoginAsync(LoginData data)
            => Accounts.LoginAsync(data, "Management");
        /// <summary>Loads the institutional dashboard summary, subject to controller authorization.</summary>
        public Task<ManagementSummary> GetSummaryAsync() => Accounts.SummaryAsync();
        #endregion Task
    }
}
