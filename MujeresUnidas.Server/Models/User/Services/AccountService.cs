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
public sealed class AccountService(UserRepository repository,IConfiguration configuration):MujeresUnidas.Server.Models.User.Contracts.IAccountSession

{
    /// <summary>A suggested verification visit has no automatic deadline or appointment date.</summary>
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
        if(!data.EligibilityAffirmed)return "ELIGIBILITY_REQUIRED";
        if(string.IsNullOrWhiteSpace(data.Password) || data.Password.Length<10 ||
           string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length<2 ||
           string.IsNullOrWhiteSpace(data.Curp) || data.Curp.Trim().Length!=18 ||
           string.IsNullOrWhiteSpace(data.Email)) return "INVALID_INPUT";
        var (salt,hash)=PasswordSecurity.Hash(data.Password);
        return await repository.InsertCommunityAsync(data,salt,hash,null);
    }

    public Task<IReadOnlyList<CommunityAccountRow>> AllCommunityAsync()=>repository.AllCommunityAsync();

    public async Task<ApiUser?> UpdateProfileAsync(Guid accountId,CommunityMember.ProfileData data)
    {
        if(string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length<2)return null;
        return await repository.UpdateCommunityProfileAsync(accountId,data);
    }

    public async Task<ApiUser?> UpdateInstitutionalProfileAsync(Guid id,ManagementUser.ProfileData data)
    {
        if(string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length is < 2 or > 100 ||
           (data.Biography?.Length??0)>300)return null;
        return await repository.UpdateInstitutionalProfileAsync(id,data);
    }

    /// <summary>Authenticates either client using the same credentials contract.</summary>
    public async Task<(AuthResponse? response,string status)> LoginAsync(BaseUser.LoginData data, string? requiredKind = null)
    {
        // Fail closed before authenticating: an older SQL schema cannot store scoped sessions.
        // Never let a missing column become an unhandled SQL 500 in either client.
        try
        {
            if (!await repository.SchemaCurrentAsync()) return (null,"SCHEMA_OUTDATED");
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            return (null,"DATABASE_UNAVAILABLE");
        }
        if(string.IsNullOrWhiteSpace(data.Email)||string.IsNullOrWhiteSpace(data.Password))return(null,"INVALID_CREDENTIALS");
        var account=await repository.FindByEmailAsync(data.Email.Trim().ToLowerInvariant());
        if(account is null || !PasswordSecurity.Verify(data.Password,account.PasswordSalt,account.PasswordHash))
            return(null,"INVALID_CREDENTIALS");
        if(account.Status!=(byte)AccountStatus.Active)
            return(null,account.Status==(byte)AccountStatus.Pending?"PENDING":"BLOCKED");
        if(requiredKind == "Owner" && account.Kind != (byte)AccountKind.Owner)
            return (null, "WRONG_APPLICATION");
        var application=requiredKind=="Community"?"Community":"Management";
        var access=await repository.AccessAsync(account.Id,application);
        if(access is null)return (null,"WRONG_APPLICATION");
        if(access.Value.Status!="Active")return (null,access.Value.Status=="Pending"?"PENDING":"BLOCKED");
        string token=PasswordSecurity.NewToken();
        var expires=DateTime.UtcNow.AddHours(8);
        try
        {
            await repository.SaveSessionAsync(account.Id,PasswordSecurity.TokenDigest(token),expires,application);
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            // Schema drift or an unavailable database must not become an HTTP 500.
            return (null,"DATABASE_UNAVAILABLE");
        }
        var actor=account.ToApiUser();
        if(application=="Community")actor=actor with {Kind="Community",Role=null,CommunityRole=access.Value.Role};
        return(new AuthResponse(token,expires,actor),"OK");
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
    /// <summary>Approval must specify the community role. Registration cannot grant Staff.</summary>
    public Task<bool> ChangeStatusAsync(Guid accountId,AccountStatus status,Guid actorId,string? communityRole=null)
    {
        if(status==AccountStatus.Active && communityRole is not ("Member" or "Staff"))
            return Task.FromResult(false);
        return repository.ChangeStatusAsync(accountId,status,actorId,communityRole);
    }

    public async Task<string> CreateStaffAsync(ManagementUser.StaffRegistrationData data,Guid actor)
    {
        if(string.IsNullOrWhiteSpace(data.Role) || data.Role.Length>64 ||
           !await repository.RoleExistsAsync(data.Role.Trim())) return "INVALID_ROLE";
        if(string.IsNullOrWhiteSpace(data.Password) || data.Password.Length<10 ||
           string.IsNullOrWhiteSpace(data.DisplayName) || data.DisplayName.Trim().Length<2 ||
           string.IsNullOrWhiteSpace(data.Email)) return "INVALID_INPUT";
        var (salt,hash)=PasswordSecurity.Hash(data.Password);
        return await repository.InsertStaffAsync(data,salt,hash,actor,null);
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
    /// <summary>Private account details are never included in public search/profile responses.</summary>
    public Task<global::MujeresUnidas.Server.Models.User.Base.User.PersonalDetailsData?> MyDetailsAsync(Guid id)=>repository.MyDetailsAsync(id);
    public Task<bool> UpdateMyDetailsAsync(Guid id,global::MujeresUnidas.Server.Models.User.Base.User.PersonalDetailsData data)
    {
        if(string.IsNullOrWhiteSpace(data.DisplayName)||data.DisplayName.Trim().Length is <2 or >100||
            (data.DateOfBirth is DateTime birth && (birth.Date>DateTime.UtcNow.Date || birth.Year<1900)) ||
            (!string.IsNullOrWhiteSpace(data.Curp) && data.Curp.Trim().Length!=18) ||
            (!string.IsNullOrWhiteSpace(data.Rfc) && data.Rfc.Trim().Length is <12 or >13))
            return Task.FromResult(false);
        return repository.UpdateMyDetailsAsync(id,data);
    }
    public Task<bool> ChangePasswordAsync(Guid id,global::MujeresUnidas.Server.Models.User.Base.User.ChangePasswordData data)
    {
        if(string.IsNullOrWhiteSpace(data.CurrentPassword)||string.IsNullOrWhiteSpace(data.NewPassword)||data.NewPassword.Length is <10 or >128 ||
           data.CurrentPassword==data.NewPassword)return Task.FromResult(false);
        return repository.ChangePasswordAsync(id,data.CurrentPassword,data.NewPassword);
    }
    public Task<bool> VerifyDemoIdentityAsync(Guid accountId,Guid reviewer,Guid? siteId=null)=>repository.VerifyDemoIdentityAsync(accountId,reviewer,siteId);
    public Task<IReadOnlyList<(Guid Id,string Name,string Email,string Role,string Status)>> StaffAccountsAsync()=>repository.StaffAccountsAsync();
    public Task<bool> ActivateStaffAsync(Guid id,Guid reviewer)=>repository.ActivateStaffAsync(id,reviewer);
    #endregion Task
}
}
