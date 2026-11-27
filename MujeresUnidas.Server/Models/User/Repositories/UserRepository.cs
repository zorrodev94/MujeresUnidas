using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Server.Models.User.Classes;
using MujeresUnidas.Server.Models.User.Enums;
using MujeresUnidas.Server.Models.User.Subclasses;

namespace MujeresUnidas.Server.Models.User.Repositories
{
/// <summary>Provides all account persistence and audit operations against SQL Server.</summary>
public sealed class UserRepository(Database database)
{
    #region Task
    /// <summary>Checks that a local demo database matches this revision without modifying it.</summary>
    internal async Task<bool> SchemaCurrentAsync()
    {
        await using var db = await database.OpenAsync();
        await using var cmd = new SqlCommand(@"SELECT CASE WHEN
OBJECT_ID(N'dbo.ManagementRoles',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.Accounts',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.CommunityPosts',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.CareSites',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.ApplicationAccess',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.ApplicationProfiles',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.ServiceZones',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.MetricGoals',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.CommunityOutcomeSnapshots',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.CommunitySurveys',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.CommunitySurveyResponses',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.MessageAttachments',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.MediaAssets',N'U') IS NOT NULL AND
OBJECT_ID(N'dbo.StorageQuotas',N'U') IS NOT NULL AND
COL_LENGTH(N'dbo.Sessions',N'ApplicationCode') IS NOT NULL AND
COL_LENGTH(N'dbo.Accounts',N'CommunityEligibilityDeclared') IS NOT NULL AND
EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Accounts')
AND name=N'CommunityRole') AND
EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Accounts')
AND name=N'StaffRole' AND is_nullable=1)
THEN 1 ELSE 0 END",db);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
    }

    /// <summary>Revokes the current login session by its stored token digest.</summary>
    internal async Task LogoutAsync(byte[] digest)
    {
        await using var db = await database.OpenAsync();
        await using var cmd = new SqlCommand("DELETE FROM dbo.Sessions WHERE TokenDigest=@digest",db);
        cmd.Parameters.Add("@digest",SqlDbType.VarBinary,32).Value=digest;
        await cmd.ExecuteNonQueryAsync();
    }

    internal async Task<bool> OwnerExistsAsync()
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand("SELECT COUNT(1) FROM dbo.Accounts WHERE Kind=2",db);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync())>0;
    }

    internal async Task<AccountSnapshot?> FindByEmailAsync(string normalizedEmail)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT Id,Email,DisplayName,Curp,Biography,Kind,Status,StaffRole,CommunityRole,PasswordSalt,PasswordHash,MustChangePassword
FROM dbo.Accounts WHERE Email=@email",db);
        cmd.Parameters.AddWithValue("@email",normalizedEmail);
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        return new AccountSnapshot(r.GetGuid(0),r.GetString(1),r.GetString(2),
            r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),
            r.GetByte(5),r.GetByte(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),(byte[])r[9],(byte[])r[10],r.GetBoolean(11));
    }

    internal async Task<(string Status,string Role)?> AccessAsync(Guid accountId,string application)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT AccessStatus,RoleCode FROM dbo.ApplicationAccess
WHERE AccountId=@id AND ApplicationCode=@app",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
        cmd.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
        await using var r=await cmd.ExecuteReaderAsync();
        return await r.ReadAsync()?(r.GetString(0),r.GetString(1)):null;
    }

    internal async Task<ApiUser?> GetByTokenAsync(byte[] digest)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT a.Id,a.Email,a.DisplayName,a.Curp,a.Biography,a.Kind,a.Status,a.StaffRole,a.CommunityRole,a.MustChangePassword,s.ApplicationCode,x.RoleCode
FROM dbo.Sessions s JOIN dbo.Accounts a ON s.AccountId=a.Id
JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id AND x.ApplicationCode=s.ApplicationCode AND x.AccessStatus=N'Active'
WHERE s.TokenDigest=@digest AND s.ExpiresUtc>SYSUTCDATETIME() AND a.Status=1",db);
        cmd.Parameters.Add("@digest",SqlDbType.VarBinary,32).Value=digest;
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        bool community=r.GetString(10)=="Community";
        return new ApiUser(r.GetGuid(0),r.GetString(1),r.GetString(2),"Active",
            community?"Community":r.GetByte(5)==2?"Owner":"Staff",
            r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),
            community?null:(r.IsDBNull(7)?null:r.GetString(7)),
            community?r.GetString(11):null,r.GetBoolean(9));
    }

    internal async Task<IReadOnlyList<CommunityAccountRow>> AllCommunityAsync()
    {
        var rows=new List<CommunityAccountRow>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT a.Id,a.Email,a.DisplayName,a.Curp,a.Status,a.ActivationStatus,a.CreatedUtc,a.CommunityRole,
v.ProposedUtc,v.IdentityChecked,v.LocationLabel
FROM dbo.Accounts a LEFT JOIN dbo.ActivationVisits v ON v.AccountId=a.Id
WHERE a.Kind=0 ORDER BY a.CreatedUtc DESC",db);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())
            rows.Add(new CommunityAccountRow(r.GetGuid(0),r.GetString(1),r.GetString(2),
                r.IsDBNull(3)?null:r.GetString(3),StatusName(r.GetByte(4)),
                ActivationStatusName(r.GetByte(5)),r.GetDateTime(6),r.GetString(7),
                r.IsDBNull(8)?null:r.GetDateTime(8),!r.IsDBNull(9)&&r.GetBoolean(9),r.IsDBNull(10)?null:r.GetString(10)));
        return rows;
    }

    internal async Task<ManagementSummary> SummaryAsync()
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT
SUM(CASE WHEN Kind=0 AND Status=0 THEN 1 ELSE 0 END),
SUM(CASE WHEN Kind=0 AND Status=1 THEN 1 ELSE 0 END),
SUM(CASE WHEN Kind=0 AND Status=2 THEN 1 ELSE 0 END),
SUM(CASE WHEN Kind=1 THEN 1 ELSE 0 END) FROM dbo.Accounts",db);
        int pending=0,active=0,blocked=0,staff=0;
        await using(var r=await cmd.ExecuteReaderAsync())
        {
            if(await r.ReadAsync())
            {
                pending=r.IsDBNull(0)?0:r.GetInt32(0);
                active=r.IsDBNull(1)?0:r.GetInt32(1);
                blocked=r.IsDBNull(2)?0:r.GetInt32(2);
                staff=r.IsDBNull(3)?0:r.GetInt32(3);
            }
        }
        var activity=new List<ManagementEvent>();
        await using var audit=new SqlCommand(@"SELECT TOP(5) e.Action,a.DisplayName,e.CreatedUtc
FROM dbo.AuditEvents e JOIN dbo.Accounts a ON e.AccountId=a.Id ORDER BY e.CreatedUtc DESC",db);
        await using var a=await audit.ExecuteReaderAsync();
        while(await a.ReadAsync()) activity.Add(new ManagementEvent(a.GetString(0),a.GetString(1),a.GetDateTime(2)));
        return new ManagementSummary(pending,active,blocked,staff,activity);
    }
    /// <summary>Returns role options managed in SQL Server.</summary>
    internal async Task<IReadOnlyList<ManagementRole>> AllRolesAsync()
    {
        var roles=new List<ManagementRole>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT Code,DisplayName FROM dbo.ManagementRoles
WHERE IsActive=1 ORDER BY DisplayName",db);
        await using var reader=await cmd.ExecuteReaderAsync();
        while(await reader.ReadAsync()) roles.Add(new ManagementRole(reader.GetString(0),reader.GetString(1)));
        return roles;
    }

    /// <summary>Checks against persisted roles instead of a compiled enum.</summary>
    internal async Task<bool> RoleExistsAsync(string role)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT COUNT(1) FROM dbo.ManagementRoles
WHERE Code=@role AND IsActive=1",db);
        cmd.Parameters.AddWithValue("@role",role);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
    }

    /// <summary>Adds a management role option; custom roles inherit no administrative permissions.</summary>
    internal async Task<string> AddRoleAsync(string displayName)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"INSERT dbo.ManagementRoles(Code,DisplayName)
VALUES(@code,@name)",db);
        var code="Custom_"+Guid.NewGuid().ToString("N");
        cmd.Parameters.AddWithValue("@code",code);
        cmd.Parameters.AddWithValue("@name",displayName.Trim());
        try
        {
            await cmd.ExecuteNonQueryAsync();
            return "OK";
        }
        catch(SqlException ex) when(ex.Number is 2601 or 2627) {return "DUPLICATE";}
    }


    internal async Task<string> InsertOwnerAsync(OwnerUser.SetupData data,byte[] salt,byte[] hash)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using var cmd=new SqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.Accounts WITH(UPDLOCK,HOLDLOCK) WHERE Kind=2)
SELECT CAST(0 AS INT)
ELSE BEGIN
INSERT dbo.Accounts(Id,Email,DisplayName,Kind,StaffRole,Status,PasswordSalt,PasswordHash)
VALUES(NEWID(),@email,@name,2,NULL,1,@salt,@hash)
SELECT CAST(1 AS INT)
END",db,tx);
            cmd.Parameters.AddWithValue("@email",data.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@name",data.DisplayName.Trim());
            cmd.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=salt;
            cmd.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hash;
            int created=Convert.ToInt32(await cmd.ExecuteScalarAsync());
            if(created==1)
            {
                await using var access=new SqlCommand(@"INSERT dbo.ApplicationAccess(AccountId,ApplicationCode,RoleCode,AccessStatus,GrantedUtc)
SELECT Id,N'Management',N'Owner',N'Active',SYSUTCDATETIME() FROM dbo.Accounts WHERE Kind=2",db,tx);
                await access.ExecuteNonQueryAsync();
                await using var profile=new SqlCommand(@"INSERT dbo.ApplicationProfiles(AccountId,ApplicationCode,DisplayName)
SELECT Id,N'Management',DisplayName FROM dbo.Accounts WHERE Kind=2",db,tx);
                await profile.ExecuteNonQueryAsync();
            }
            await tx.CommitAsync();
            return created==1?"OK":"ALREADY_CONFIGURED";
        }
        catch(SqlException e) when(e.Number is 2601 or 2627)
        {
            await tx.RollbackAsync();return "ALREADY_CONFIGURED";
        }
        catch(SqlException e) when(e.Number==515 && e.Message.Contains("StaffRole",StringComparison.OrdinalIgnoreCase))
        {
            await tx.RollbackAsync();return "SCHEMA_OUTDATED";
        }
    }

    internal async Task<string> InsertCommunityAsync(CommunityMember.RegistrationData data,byte[] salt,byte[] hash,DateTime? proposedVisitUtc)
    {
        await using var db=await database.OpenAsync();
        try
        {
            await using var cmd=new SqlCommand(@"INSERT dbo.Accounts(Id,Email,DisplayName,Curp,Kind,StaffRole,CommunityRole,Status,PasswordSalt,PasswordHash,CommunityEligibilityDeclared)
VALUES(NEWID(),@email,@name,@curp,0,NULL,N'Member',0,@salt,@hash,1)",db);
            cmd.Parameters.AddWithValue("@email",data.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@name",data.DisplayName.Trim());
            cmd.Parameters.AddWithValue("@curp",data.Curp.Trim().ToUpperInvariant());
            cmd.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=salt;
            cmd.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hash;
            await cmd.ExecuteNonQueryAsync();
            await using(var access=new SqlCommand(@"INSERT dbo.ApplicationAccess(AccountId,ApplicationCode,RoleCode,AccessStatus)
SELECT Id,N'Community',N'Member',N'Pending' FROM dbo.Accounts WHERE Email=@email",db))
            {
                access.Parameters.Add("@email",SqlDbType.NVarChar,254).Value=data.Email.Trim().ToLowerInvariant();
                await access.ExecuteNonQueryAsync();
            }
            await using(var profile=new SqlCommand(@"INSERT dbo.ApplicationProfiles(AccountId,ApplicationCode,DisplayName)
SELECT Id,N'Community',DisplayName FROM dbo.Accounts WHERE Email=@email",db))
            {
                profile.Parameters.Add("@email",SqlDbType.NVarChar,254).Value=data.Email.Trim().ToLowerInvariant();
                await profile.ExecuteNonQueryAsync();
            }
            await using var visit=new SqlCommand(@"INSERT dbo.ActivationVisits(AccountId,ProposedUtc,LocationLabel)
SELECT Id,@when,N'Sede por confirmar (simulación)' FROM dbo.Accounts WHERE Email=@email",db);
            visit.Parameters.Add("@email",SqlDbType.NVarChar,254).Value=data.Email.Trim().ToLowerInvariant();
            visit.Parameters.Add("@when",SqlDbType.DateTime2).Value=(object?)proposedVisitUtc??DBNull.Value;
            await visit.ExecuteNonQueryAsync();
            return "OK";
        }
        catch(SqlException e) when(e.Number is 2601 or 2627){return "DUPLICATE";}
        catch(SqlException e) when(e.Number==515 && e.Message.Contains("StaffRole",StringComparison.OrdinalIgnoreCase))
        {return "SCHEMA_OUTDATED";}
    }

    internal async Task<string> InsertStaffAsync(ManagementUser.StaffRegistrationData data,byte[] salt,byte[] hash,Guid actor,DateTime? proposedVisitUtc)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        try
        {
            Guid accountId=Guid.NewGuid();
            await using var cmd=new SqlCommand(@"INSERT dbo.Accounts(Id,Email,DisplayName,Kind,StaffRole,Status,PasswordSalt,PasswordHash,MustChangePassword)
VALUES(@id,@email,@name,1,@role,0,@salt,@hash,1)",db,tx);
            cmd.Parameters.AddWithValue("@id",accountId);
            cmd.Parameters.AddWithValue("@email",data.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@name",data.DisplayName.Trim());
            cmd.Parameters.AddWithValue("@role",data.Role.Trim());
            cmd.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=salt;
            cmd.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hash;
            cmd.Parameters.AddWithValue("@actor",actor);
            await cmd.ExecuteNonQueryAsync();
            await using(var access=new SqlCommand(@"INSERT dbo.ApplicationAccess(AccountId,ApplicationCode,RoleCode,AccessStatus)
VALUES(@id,N'Management',@role,N'Pending')",db,tx))
            {
                access.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
                access.Parameters.Add("@role",SqlDbType.NVarChar,64).Value=data.Role.Trim();
                await access.ExecuteNonQueryAsync();
            }
            await using(var profile=new SqlCommand(@"INSERT dbo.ApplicationProfiles(AccountId,ApplicationCode,DisplayName)
VALUES(@id,N'Management',@name)",db,tx))
            {
                profile.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
                profile.Parameters.Add("@name",SqlDbType.NVarChar,100).Value=data.DisplayName.Trim();
                await profile.ExecuteNonQueryAsync();
            }
            await using var visit=new SqlCommand(@"INSERT dbo.ActivationVisits(AccountId,ProposedUtc,LocationLabel)
VALUES(@id,@when,N'Sede por confirmar (simulación)')",db,tx);
            visit.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
            visit.Parameters.Add("@when",SqlDbType.DateTime2).Value=(object?)proposedVisitUtc??DBNull.Value;
            await visit.ExecuteNonQueryAsync();
            await using var audit=new SqlCommand(@"INSERT dbo.AuditEvents(Id,AccountId,ActorId,Action)
VALUES(NEWID(),@id,@actor,'STAFF_INVITE')",db,tx);
            audit.Parameters.AddWithValue("@id",accountId);
            audit.Parameters.AddWithValue("@actor",actor);
            await audit.ExecuteNonQueryAsync();
            await tx.CommitAsync();return "OK";
        }
        catch(SqlException e) when(e.Number is 2601 or 2627)
        {
            await tx.RollbackAsync();return "DUPLICATE";
        }
        catch(SqlException e) when(e.Number==515 && e.Message.Contains("StaffRole",StringComparison.OrdinalIgnoreCase))
        {
            await tx.RollbackAsync();return "SCHEMA_OUTDATED";
        }
    }

    internal async Task SaveSessionAsync(Guid accountId,byte[] tokenDigest,DateTime expiresUtc,string application)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"INSERT dbo.Sessions(TokenDigest,AccountId,ExpiresUtc,ApplicationCode)
VALUES(@digest,@id,@expires,@app)",db);
        cmd.Parameters.Add("@digest",SqlDbType.VarBinary,32).Value=tokenDigest;
        cmd.Parameters.AddWithValue("@id",accountId);
        cmd.Parameters.AddWithValue("@expires",expiresUtc);
        cmd.Parameters.Add("@app",SqlDbType.NVarChar,12).Value=application;
        await cmd.ExecuteNonQueryAsync();
    }

    internal async Task<bool> ChangeStatusAsync(Guid accountId,AccountStatus status,Guid actorId,string? communityRole=null)
    {
        if(status is not (AccountStatus.Active or AccountStatus.Blocked))return false;
        if(status==AccountStatus.Active && communityRole is not ("Member" or "Staff"))return false;
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.Accounts
SET Status=@status,
CommunityRole=CASE WHEN @status=@active THEN @role ELSE CommunityRole END,
ActivationStatus=CASE WHEN @status=@active THEN @approved ELSE ActivationStatus END,
ActivatedBy=CASE WHEN @status=@active THEN @actor ELSE ActivatedBy END,
ActivatedUtc=CASE WHEN @status=@active THEN SYSUTCDATETIME() ELSE ActivatedUtc END
WHERE Id=@id AND Kind=0 AND Status<>@status AND
(@status<>@active OR EXISTS(SELECT 1 FROM dbo.ActivationVisits
 WHERE AccountId=@id AND IdentityChecked=1))",db,tx);
        cmd.Parameters.AddWithValue("@status",(byte)status);
        cmd.Parameters.Add("@role",SqlDbType.NVarChar,12).Value=(object?)communityRole??DBNull.Value;
        cmd.Parameters.AddWithValue("@active",(byte)AccountStatus.Active);
        cmd.Parameters.AddWithValue("@approved",(byte)ActivationStatus.Approved);
        cmd.Parameters.AddWithValue("@actor",actorId);
        cmd.Parameters.AddWithValue("@id",accountId);
        bool changed=await cmd.ExecuteNonQueryAsync()==1;
        if(changed)
        {
            await using var access=new SqlCommand(@"UPDATE dbo.ApplicationAccess SET AccessStatus=@appstatus,
 RoleCode=CASE WHEN @appstatus=N'Active' THEN @role ELSE RoleCode END,
 GrantedBy=CASE WHEN @appstatus=N'Active' THEN @actor ELSE GrantedBy END,
 GrantedUtc=CASE WHEN @appstatus=N'Active' THEN SYSUTCDATETIME() ELSE GrantedUtc END
 WHERE AccountId=@id AND ApplicationCode=N'Community'",db,tx);
            access.Parameters.Add("@appstatus",SqlDbType.NVarChar,12).Value=status==AccountStatus.Active?"Active":"Suspended";
            access.Parameters.Add("@role",SqlDbType.NVarChar,64).Value=(object?)communityRole??DBNull.Value;
            access.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
            access.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
            await access.ExecuteNonQueryAsync();
            await using var audit=new SqlCommand(@"INSERT dbo.AuditEvents(Id,AccountId,ActorId,Action)
VALUES(NEWID(),@id,@actor,@action)",db,tx);
            audit.Parameters.AddWithValue("@id",accountId);
            audit.Parameters.AddWithValue("@actor",actorId);
            audit.Parameters.AddWithValue("@action",status==AccountStatus.Active?$"ACTIVATE_{communityRole?.ToUpperInvariant()}":"BLOCK");
            await audit.ExecuteNonQueryAsync();
            if(status==AccountStatus.Active)
            {
                await using var notification=new SqlCommand(@"INSERT dbo.Notifications(Id,RecipientId,ActorId,Type,Title,RelatedId)
VALUES(NEWID(),@id,@actor,N'Activation',N'Tu cuenta fue activada.',@id)",db,tx);
                notification.Parameters.AddWithValue("@id",accountId);
                notification.Parameters.AddWithValue("@actor",actorId);
                await notification.ExecuteNonQueryAsync();
            }
        }
        await tx.CommitAsync();return changed;
    }

    internal async Task<ApiUser?> UpdateInstitutionalProfileAsync(Guid id,ManagementUser.ProfileData data)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.Accounts SET DisplayName=@name,Biography=@bio
WHERE Id=@id AND Kind IN (1,2) AND Status=1",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        cmd.Parameters.Add("@name",SqlDbType.NVarChar,100).Value=data.DisplayName.Trim();
        cmd.Parameters.Add("@bio",SqlDbType.NVarChar,300).Value=(object?)data.Biography?.Trim()??DBNull.Value;
        if(await cmd.ExecuteNonQueryAsync()!=1)return null;
        await using(var sync=new SqlCommand(@"UPDATE dbo.ApplicationProfiles SET DisplayName=@name,Biography=@bio,UpdatedUtc=SYSUTCDATETIME()
WHERE AccountId=@id AND ApplicationCode=N'Management'",db))
        {
            sync.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            sync.Parameters.Add("@name",SqlDbType.NVarChar,100).Value=data.DisplayName.Trim();
            sync.Parameters.Add("@bio",SqlDbType.NVarChar,500).Value=(object?)data.Biography?.Trim()??DBNull.Value;
            await sync.ExecuteNonQueryAsync();
        }
        await using var get=new SqlCommand(@"SELECT Email,DisplayName,Curp,Biography,Kind,StaffRole
FROM dbo.Accounts WHERE Id=@id",db);
        get.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        await using var r=await get.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        return new ApiUser(id,r.GetString(0),r.GetString(1),"Active",r.GetByte(4)==2?"Owner":"Staff",
            r.IsDBNull(2)?null:r.GetString(2),r.IsDBNull(3)?null:r.GetString(3),
            r.IsDBNull(5)?null:r.GetString(5));
    }

    internal async Task<ApiUser?> UpdateCommunityProfileAsync(Guid id,CommunityMember.ProfileData data)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.ApplicationProfiles SET DisplayName=@name,Biography=@bio,UpdatedUtc=SYSUTCDATETIME()
WHERE AccountId=@id AND ApplicationCode=N'Community'
AND EXISTS(SELECT 1 FROM dbo.ApplicationAccess x JOIN dbo.Accounts a ON a.Id=x.AccountId
WHERE x.AccountId=@id AND x.ApplicationCode=N'Community' AND x.AccessStatus=N'Active' AND a.Status=1);
IF @@ROWCOUNT=1 BEGIN
 UPDATE dbo.Accounts SET DisplayName=@name,Biography=@bio WHERE Id=@id AND Kind=0;
 SELECT 1;
END ELSE SELECT 0;",db);
        cmd.Parameters.AddWithValue("@name",data.DisplayName.Trim());
        cmd.Parameters.AddWithValue("@bio",(object?)data.Biography?.Trim()??DBNull.Value);
        cmd.Parameters.AddWithValue("@id",id);
        if(Convert.ToInt32(await cmd.ExecuteScalarAsync())!=1)return null;
        await using var get=new SqlCommand(@"SELECT a.Email,a.Curp,p.Biography,p.DisplayName,x.RoleCode
FROM dbo.Accounts a JOIN dbo.ApplicationProfiles p ON p.AccountId=a.Id AND p.ApplicationCode=N'Community'
JOIN dbo.ApplicationAccess x ON x.AccountId=a.Id AND x.ApplicationCode=N'Community'
WHERE a.Id=@id",db);
        get.Parameters.AddWithValue("@id",id);
        await using var r=await get.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        return new ApiUser(id,r.GetString(0),r.GetString(3),"Active","Community",
            r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),null,r.GetString(4));
    }

    /// <summary>Returns extended identity solely to the logged-in account owner.</summary>
    internal async Task<global::MujeresUnidas.Server.Models.User.Base.User.PersonalDetailsData?> MyDetailsAsync(Guid id)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT DisplayName,Biography,FirstName,PaternalLastName,MaternalLastName,
PhoneNumber,AlternatePhoneNumber,DateOfBirth,Address,City,State,PostalCode,Rfc,Curp
FROM dbo.Accounts WHERE Id=@id AND Status=1",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        string? S(int i)=>r.IsDBNull(i)?null:r.GetString(i);
        return new global::MujeresUnidas.Server.Models.User.Base.User.PersonalDetailsData
        {
            DisplayName=r.GetString(0),Biography=S(1),FirstName=S(2),PaternalLastName=S(3),MaternalLastName=S(4),
            PhoneNumber=S(5),AlternatePhoneNumber=S(6),DateOfBirth=r.IsDBNull(7)?null:r.GetDateTime(7),
            Address=S(8),City=S(9),State=S(10),PostalCode=S(11),Rfc=S(12),Curp=S(13)
        };
    }
    internal async Task<bool> UpdateMyDetailsAsync(Guid id,global::MujeresUnidas.Server.Models.User.Base.User.PersonalDetailsData data)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.Accounts SET DisplayName=@name,Biography=@bio,
FirstName=@first,PaternalLastName=@paternal,MaternalLastName=@maternal,
PhoneNumber=@phone,AlternatePhoneNumber=@alt,DateOfBirth=@birth,
Address=@address,City=@city,State=@state,PostalCode=@postal,Rfc=@rfc,
Curp=CASE WHEN @curp IS NULL THEN Curp ELSE @curp END
WHERE Id=@id AND Status=1",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        void P(string name,int length,string? text)=>cmd.Parameters.Add(name,SqlDbType.NVarChar,length).Value=
            string.IsNullOrWhiteSpace(text)?DBNull.Value:text.Trim();
        P("@name",100,data.DisplayName);P("@bio",300,data.Biography);
        P("@first",70,data.FirstName);P("@paternal",70,data.PaternalLastName);
        P("@maternal",70,data.MaternalLastName);P("@phone",24,data.PhoneNumber);
        P("@alt",24,data.AlternatePhoneNumber);P("@address",250,data.Address);
        P("@city",90,data.City);P("@state",90,data.State);
        P("@postal",12,data.PostalCode);P("@rfc",13,data.Rfc);
        P("@curp",18,data.Curp?.ToUpperInvariant());
        cmd.Parameters.Add("@birth",SqlDbType.Date).Value=data.DateOfBirth is DateTime birth?birth.Date:DBNull.Value;
        try{return await cmd.ExecuteNonQueryAsync()==1;}
        catch(SqlException e) when(e.Number is 2601 or 2627){return false;}
    }
    internal async Task<bool> ChangePasswordAsync(Guid id,string current,string replacement)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var check=new SqlCommand(@"SELECT PasswordSalt,PasswordHash FROM dbo.Accounts WITH(UPDLOCK,HOLDLOCK)
WHERE Id=@id AND Status=1",db,tx);
        check.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        byte[]? salt=null;byte[]? hash=null;
        await using(var r=await check.ExecuteReaderAsync())
        {if(await r.ReadAsync()){salt=(byte[])r[0];hash=(byte[])r[1];}}
        if(salt is null||hash is null||!global::MujeresUnidas.Server.Models.User.Services.PasswordSecurity.Verify(current,salt,hash))
        {await tx.RollbackAsync();return false;}
        var (newSalt,newHash)=global::MujeresUnidas.Server.Models.User.Services.PasswordSecurity.Hash(replacement);
        await using var update=new SqlCommand(@"UPDATE dbo.Accounts SET PasswordSalt=@salt,PasswordHash=@hash,MustChangePassword=0
WHERE Id=@id; DELETE FROM dbo.Sessions WHERE AccountId=@id",db,tx);
        update.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        update.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=newSalt;
        update.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=newHash;
        await update.ExecuteNonQueryAsync();await tx.CommitAsync();return true;
    }
    internal async Task<IReadOnlyList<(Guid Id,string Name,string Email,string Role,string Status)>> StaffAccountsAsync()
    {
        var items=new List<(Guid,string,string,string,string)>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT Id,DisplayName,Email,StaffRole,Status FROM dbo.Accounts
WHERE Kind=1 ORDER BY CreatedUtc DESC",db);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())items.Add((r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),
            r.GetByte(4)==0?"Pending":r.GetByte(4)==1?"Active":"Blocked"));
        return items;
    }
    /// <summary>Records a demo-only identity check with reviewer and audit; no document scans are stored.</summary>
    internal async Task<bool> VerifyDemoIdentityAsync(Guid id,Guid reviewer,Guid? siteId=null)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        await using var cmd=new SqlCommand(@"IF EXISTS(SELECT 1 FROM dbo.Accounts WHERE Id=@id AND Status=0)
AND (@site IS NULL OR EXISTS(SELECT 1 FROM dbo.CareSites WHERE Id=@site AND IsActive=1 AND SiteType IN(N'Administrative',N'MultiService')))
BEGIN
 IF EXISTS(SELECT 1 FROM dbo.ActivationVisits WHERE AccountId=@id)
  UPDATE dbo.ActivationVisits SET IdentityChecked=1,CheckedBy=@actor,CheckedUtc=SYSUTCDATETIME(),
   SiteId=COALESCE(@site,SiteId),LocationLabel=CASE WHEN @site IS NULL THEN LocationLabel
    ELSE (SELECT DisplayName FROM dbo.CareSites WHERE Id=@site) END WHERE AccountId=@id;
 ELSE INSERT dbo.ActivationVisits(AccountId,IdentityChecked,CheckedBy,CheckedUtc,SiteId,LocationLabel)
  VALUES(@id,1,@actor,SYSUTCDATETIME(),@site,
   (SELECT DisplayName FROM dbo.CareSites WHERE Id=@site));
 SELECT CAST(1 AS bit);
END ELSE SELECT CAST(0 AS bit)",db,tx);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=reviewer;
        cmd.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=(object?)siteId??DBNull.Value;
        bool changed=Convert.ToBoolean(await cmd.ExecuteScalarAsync());
        if(changed)
        {
            await using var audit=new SqlCommand(@"INSERT dbo.AuditEvents(Id,AccountId,ActorId,Action)
VALUES(NEWID(),@id,@actor,N'DEMO_ID_CHECK')",db,tx);
            audit.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
            audit.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=reviewer;
            await audit.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();return changed;
    }
    internal async Task<bool> ActivateStaffAsync(Guid id,Guid reviewer)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.Accounts SET Status=1,ActivationStatus=1,ActivatedBy=@actor,
ActivatedUtc=SYSUTCDATETIME() WHERE Id=@id AND Kind=1 AND Status=0 AND EXISTS
(SELECT 1 FROM dbo.ActivationVisits WHERE AccountId=@id AND IdentityChecked=1)",db,tx);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=reviewer;
        if(await cmd.ExecuteNonQueryAsync()!=1){await tx.RollbackAsync();return false;}
        await using var log=new SqlCommand(@"INSERT dbo.AuditEvents(Id,AccountId,ActorId,Action)
VALUES(NEWID(),@id,@actor,N'STAFF_ACTIVATE')",db,tx);
        log.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        log.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=reviewer;
        await log.ExecuteNonQueryAsync();
        await using var grant=new SqlCommand(@"UPDATE dbo.ApplicationAccess
SET AccessStatus=N'Active',GrantedBy=@actor,GrantedUtc=SYSUTCDATETIME()
WHERE AccountId=@id AND ApplicationCode=N'Management'",db,tx);
        grant.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=id;
        grant.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=reviewer;
        await grant.ExecuteNonQueryAsync();
        await tx.CommitAsync();return true;
    }
    #endregion Task

    private static string StatusName(byte state)=>state switch
    {
        (byte)AccountStatus.Pending=>"Pending",
        (byte)AccountStatus.Active=>"Active",
        _=>"Blocked"
    };
    private static string ActivationStatusName(byte state)=>state switch
    {
        (byte)ActivationStatus.AwaitingAppointment=>"En espera de cita",
        (byte)ActivationStatus.Approved=>"Aprobada",
        _=>"Rechazada"
    };

}
}
