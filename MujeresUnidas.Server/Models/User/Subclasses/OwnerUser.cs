using System.ComponentModel.DataAnnotations;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Contracts;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.User.Enums;
using BaseUser = global::MujeresUnidas.Server.Models.User.Base.User;

namespace MujeresUnidas.Server.Models.User.Subclasses
{
    /// <summary>Describes the only account created through the server's initial setup key.</summary>
    public sealed class OwnerUser : BaseUser, IAccountAccess<OwnerUser.SetupData>
    {
        #region Initialization
        public OwnerUser(AccountService accounts) : base(accounts) { }
        #endregion Initialization
        #region Structs
        /// <summary>Fields shown only during the initial configuration of Gestión.</summary>
        public struct SetupData
        {
            [Required, StringLength(100, MinimumLength = 2)]
            public string DisplayName { get; set; }

            [Required, EmailAddress, StringLength(254)]
            public string Email { get; set; }

            [Required, StringLength(128, MinimumLength = 10)]
            public string Password { get; set; }

            [Required, MinLength(24)]
            public string SetupKey { get; set; }
        }
        /// <summary>Display name of a new institutional position added by the owner.</summary>
        public struct RoleRegistrationData
        {
            [Required, StringLength(100, MinimumLength = 3)]
            public string DisplayName { get; set; }
        }
        #endregion Structs
        #region Task
        /// <summary>Authenticates the owner account, never a staff or community account.</summary>
        public override Task<(AuthResponse? Response, string Status)> LoginAsync(LoginData data)
            => Accounts.LoginAsync(data, "Owner");

        /// <summary>Creates the initial owner once using the server setup key.</summary>
        public Task<string> RegisterAsync(SetupData data) => Accounts.SetupOwnerAsync(data);

        /// <summary>Approves a community account after a demonstration-only review.</summary>
        public Task<bool> AcceptAccountAsync(Guid communityAccountId, Guid ownerId,string communityRole)
            => Accounts.ChangeStatusAsync(communityAccountId, AccountStatus.Active, ownerId,communityRole);

        /// <summary>Creates a staff account after the owner has authenticated.</summary>
        public Task<string> CreateStaffAsync(ManagementUser.StaffRegistrationData data, Guid actorId)
            => Accounts.CreateStaffAsync(data, actorId);
        /// <summary>Lists the current roles available when adding institutional personnel.</summary>
        public Task<IReadOnlyList<ManagementRole>> GetRolesAsync() => Accounts.RolesAsync();

        /// <summary>Adds a new non-privileged institutional position.</summary>
        public Task<string> AddRoleAsync(RoleRegistrationData data)
            => Accounts.AddRoleAsync(data.DisplayName);
        #endregion Task
    }
}
