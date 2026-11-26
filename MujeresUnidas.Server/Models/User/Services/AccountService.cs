using System.Security.Cryptography;
using System.Text;
using MujeresUnidas.Server.Contracts;
using BaseUser = global::MujeresUnidas.Server.Models.User.Base.User;
using MujeresUnidas.Server.Models.User.Enums;
using MujeresUnidas.Server.Models.User.Repositories;
using MujeresUnidas.Server.Models.User.Subclasses;

namespace MujeresUnidas.Server.Models.User.Services
{
/// <summary>Orchestrates account rules and authorization while the repository owns SQL persistence.</summary>
public sealed class AccountService(UserRepository repository,IConfiguration configuration)

{
    #region Task
    public Task<bool> OwnerExistsAsync()=>repository.OwnerExistsAsync();
    public Task<bool> SchemaCurrentAsync()=>repository.SchemaCurrentAsync();

    public async Task<string> SetupOwnerAsync(OwnerUser.SetupData data)
    {
        string? secret=configuration["MU_OWNER_SETUP_KEY"];
        if(string.IsNullOrWhiteSpace(secret)||secret.Length<24)return "SETUP_KEY_MISSING";
        if(string.IsNullOrWhiteSpace(data.SetupKey))return "INVALID_SETUP_KEY";
        var known=SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var given=SHA256.HashData(Encoding.UTF8.GetBytes(data.SetupKey));
        if(!CryptographicOperations.FixedTimeEquals(known,given))return "INVALID_SETUP_KEY";
        if(string.IsNullOrWhiteSpace(data.Password) || data.Password.Length<10 ||
           string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length<2 ||
           string.IsNullOrWhiteSpace(data.Email)) return "INVALID_INPUT";
        var (salt,hash)=PasswordSecurity.Hash(data.Password);
        return await repository.InsertOwnerAsync(data,salt,hash);
    }

    /// <summary>Registers a community account as Pending without granting access.</summary>
    public async Task<string> RegisterAsync(CommunityMember.RegistrationData data)
    {
        if(string.IsNullOrWhiteSpace(data.Password) || data.Password.Length<10 ||
           string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length<2 ||
           string.IsNullOrWhiteSpace(data.Curp) || data.Curp.Trim().Length!=18 ||
           string.IsNullOrWhiteSpace(data.Email) ||
           data.CommunityRole is not ("Member" or "Staff")) return "INVALID_INPUT";
        var (salt,hash)=PasswordSecurity.Hash(data.Password);
        return await repository.InsertCommunityAsync(data,salt,hash);
    }

    public Task<IReadOnlyList<CommunityAccountRow>> AllCommunityAsync()=>repository.AllCommunityAsync();

    public async Task<ApiUser?> UpdateProfileAsync(Guid accountId,CommunityMember.ProfileData data)
    {
        if(string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length<2)return null;
        return await repository.UpdateCommunityProfileAsync(accountId,data);
    }

    /// <summary>Authenticates either client using the same credentials contract.</summary>
    public async Task<(AuthResponse? response,string status)> LoginAsync(BaseUser.LoginData data, string? requiredKind = null)
    {
        if(string.IsNullOrWhiteSpace(data.Email)||string.IsNullOrWhiteSpace(data.Password))return(null,"INVALID_CREDENTIALS");
        var account=await repository.FindByEmailAsync(data.Email.Trim().ToLowerInvariant());
        if(account is null || !PasswordSecurity.Verify(data.Password,account.PasswordSalt,account.PasswordHash))
            return(null,"INVALID_CREDENTIALS");
        if(account.Status!=(byte)AccountStatus.Active)
            return(null,account.Status==(byte)AccountStatus.Pending?"PENDING":"BLOCKED");
        if(requiredKind == "Community" && account.Kind != (byte)AccountKind.Community)
            return (null, "WRONG_APPLICATION");
        if(requiredKind == "Management" && account.Kind == (byte)AccountKind.Community)
            return (null, "WRONG_APPLICATION");
        if(requiredKind == "Owner" && account.Kind != (byte)AccountKind.Owner)
            return (null, "WRONG_APPLICATION");
        string token=PasswordSecurity.NewToken();
        var expires=DateTime.UtcNow.AddHours(8);
        await repository.SaveSessionAsync(account.Id,PasswordSecurity.TokenDigest(token),expires);
        return(new AuthResponse(token,expires,account.ToApiUser()),"OK");
    }

    /// <summary>Invalidates an active server session; closing the UI alone is insufficient.</summary>
    public Task LogoutAsync(string? bearer)
    {
        if (string.IsNullOrWhiteSpace(bearer) ||
            !bearer.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;
        string token = bearer[7..].Trim();
        return token.Length<20 ? Task.CompletedTask : repository.LogoutAsync(PasswordSecurity.TokenDigest(token));
    }

    public Task<ApiUser?> GetUserByTokenAsync(string? bearer)
    {
        if(string.IsNullOrWhiteSpace(bearer)||!bearer.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<ApiUser?>(null);
        string token=bearer[7..].Trim();
        if(token.Length<20)return Task.FromResult<ApiUser?>(null);
        return repository.GetByTokenAsync(PasswordSecurity.TokenDigest(token));
    }

    public Task<ManagementSummary> SummaryAsync()=>repository.SummaryAsync();
    public Task<bool> ChangeStatusAsync(Guid accountId,AccountStatus status,Guid actorId)=>repository.ChangeStatusAsync(accountId,status,actorId);

    public async Task<string> CreateStaffAsync(ManagementUser.StaffRegistrationData data,Guid actor)
    {
        if(string.IsNullOrWhiteSpace(data.Role) || data.Role.Length>64 ||
           !await repository.RoleExistsAsync(data.Role.Trim())) return "INVALID_ROLE";
        if(string.IsNullOrWhiteSpace(data.Password) || data.Password.Length<10 ||
           string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length<2 ||
           string.IsNullOrWhiteSpace(data.Email)) return "INVALID_INPUT";
        var (salt,hash)=PasswordSecurity.Hash(data.Password);
        return await repository.InsertStaffAsync(data,salt,hash,actor);
    }
    /// <summary>Reads the current management roles from SQL Server.</summary>
    public Task<IReadOnlyList<ManagementRole>> RolesAsync()=>repository.AllRolesAsync();

    /// <summary>Adds a configurable staff role without changing source code.</summary>
    public Task<string> AddRoleAsync(string displayName)
    {
        if(string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length<3 ||
            displayName.Trim().Length>100) return Task.FromResult("INVALID_INPUT");
        return repository.AddRoleAsync(displayName);
    }
    #endregion Task
}
}
