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
        await using var cmd=new SqlCommand(@"SELECT Id,Email,DisplayName,Curp,Biography,Kind,Status,StaffRole,CommunityRole,PasswordSalt,PasswordHash
FROM dbo.Accounts WHERE Email=@email",db);
        cmd.Parameters.AddWithValue("@email",normalizedEmail);
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        return new AccountSnapshot(r.GetGuid(0),r.GetString(1),r.GetString(2),
            r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),
            r.GetByte(5),r.GetByte(6),r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetString(8),(byte[])r[9],(byte[])r[10]);
    }

    internal async Task<ApiUser?> GetByTokenAsync(byte[] digest)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT a.Id,a.Email,a.DisplayName,a.Curp,a.Biography,a.Kind,a.Status,a.StaffRole,a.CommunityRole
FROM dbo.Sessions s JOIN dbo.Accounts a ON s.AccountId=a.Id
WHERE s.TokenDigest=@digest AND s.ExpiresUtc>SYSUTCDATETIME() AND a.Status=1",db);
        cmd.Parameters.Add("@digest",SqlDbType.VarBinary,32).Value=digest;
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        return new ApiUser(r.GetGuid(0),r.GetString(1),r.GetString(2),"Active",
            r.GetByte(5) switch{0=>"Community",1=>"Staff",_=>"Owner"},
            r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4),
            r.IsDBNull(7)?null:r.GetString(7),r.IsDBNull(8)?null:r.GetString(8));
    }

    internal async Task<IReadOnlyList<CommunityAccountRow>> AllCommunityAsync()
    {
        var rows=new List<CommunityAccountRow>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT Id,Email,DisplayName,Curp,Status,ActivationStatus,CreatedUtc,CommunityRole
FROM dbo.Accounts WHERE Kind=0 ORDER BY CreatedUtc DESC",db);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())
            rows.Add(new CommunityAccountRow(r.GetGuid(0),r.GetString(1),r.GetString(2),
                r.IsDBNull(3)?null:r.GetString(3),StatusName(r.GetByte(4)),
                ActivationStatusName(r.GetByte(5)),r.GetDateTime(6),r.GetString(7)));
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

    internal async Task<string> InsertCommunityAsync(CommunityMember.RegistrationData data,byte[] salt,byte[] hash)
    {
        await using var db=await database.OpenAsync();
        try
        {
            await using var cmd=new SqlCommand(@"INSERT dbo.Accounts(Id,Email,DisplayName,Curp,Kind,StaffRole,CommunityRole,Status,PasswordSalt,PasswordHash)
VALUES(NEWID(),@email,@name,@curp,0,NULL,@communityRole,0,@salt,@hash)",db);
            cmd.Parameters.AddWithValue("@email",data.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@name",data.DisplayName.Trim());
            cmd.Parameters.AddWithValue("@curp",data.Curp.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@communityRole",data.CommunityRole);
            cmd.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=salt;
            cmd.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hash;
            await cmd.ExecuteNonQueryAsync();
            return "OK";
        }
        catch(SqlException e) when(e.Number is 2601 or 2627){return "DUPLICATE";}
        catch(SqlException e) when(e.Number==515 && e.Message.Contains("StaffRole",StringComparison.OrdinalIgnoreCase))
        {return "SCHEMA_OUTDATED";}
    }

    internal async Task<string> InsertStaffAsync(ManagementUser.StaffRegistrationData data,byte[] salt,byte[] hash,Guid actor)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        try
        {
            Guid accountId=Guid.NewGuid();
            await using var cmd=new SqlCommand(@"INSERT dbo.Accounts(Id,Email,DisplayName,Kind,StaffRole,Status,PasswordSalt,PasswordHash,ActivatedBy,ActivatedUtc)
VALUES(@id,@email,@name,1,@role,1,@salt,@hash,@actor,SYSUTCDATETIME())",db,tx);
            cmd.Parameters.AddWithValue("@id",accountId);
            cmd.Parameters.AddWithValue("@email",data.Email.Trim().ToLowerInvariant());
            cmd.Parameters.AddWithValue("@name",data.DisplayName.Trim());
            cmd.Parameters.AddWithValue("@role",data.Role.Trim());
            cmd.Parameters.Add("@salt",SqlDbType.VarBinary,16).Value=salt;
            cmd.Parameters.Add("@hash",SqlDbType.VarBinary,32).Value=hash;
            cmd.Parameters.AddWithValue("@actor",actor);
            await cmd.ExecuteNonQueryAsync();
            await using var audit=new SqlCommand(@"INSERT dbo.AuditEvents(Id,AccountId,ActorId,Action)
VALUES(NEWID(),@id,@actor,'STAFF_CREATE')",db,tx);
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

    internal async Task SaveSessionAsync(Guid accountId,byte[] tokenDigest,DateTime expiresUtc)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"INSERT dbo.Sessions(TokenDigest,AccountId,ExpiresUtc)
VALUES(@digest,@id,@expires)",db);
        cmd.Parameters.Add("@digest",SqlDbType.VarBinary,32).Value=tokenDigest;
        cmd.Parameters.AddWithValue("@id",accountId);
        cmd.Parameters.AddWithValue("@expires",expiresUtc);
        await cmd.ExecuteNonQueryAsync();
    }

    internal async Task<bool> ChangeStatusAsync(Guid accountId,AccountStatus status,Guid actorId)
    {
        if(status is not (AccountStatus.Active or AccountStatus.Blocked))return false;
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.Accounts
SET Status=@status,
ActivationStatus=CASE WHEN @status=@active THEN @approved ELSE ActivationStatus END,
ActivatedBy=CASE WHEN @status=@active THEN @actor ELSE ActivatedBy END,
ActivatedUtc=CASE WHEN @status=@active THEN SYSUTCDATETIME() ELSE ActivatedUtc END
WHERE Id=@id AND Kind=0 AND Status<>@status",db,tx);
        cmd.Parameters.AddWithValue("@status",(byte)status);
        cmd.Parameters.AddWithValue("@active",(byte)AccountStatus.Active);
        cmd.Parameters.AddWithValue("@approved",(byte)ActivationStatus.Approved);
        cmd.Parameters.AddWithValue("@actor",actorId);
        cmd.Parameters.AddWithValue("@id",accountId);
        bool changed=await cmd.ExecuteNonQueryAsync()==1;
        if(changed)
        {
            await using var audit=new SqlCommand(@"INSERT dbo.AuditEvents(Id,AccountId,ActorId,Action)
VALUES(NEWID(),@id,@actor,@action)",db,tx);
            audit.Parameters.AddWithValue("@id",accountId);
            audit.Parameters.AddWithValue("@actor",actorId);
            audit.Parameters.AddWithValue("@action",status==AccountStatus.Active?"ACTIVATE":"BLOCK");
            await audit.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();return changed;
    }

    internal async Task<ApiUser?> UpdateCommunityProfileAsync(Guid id,CommunityMember.ProfileData data)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"UPDATE dbo.Accounts SET DisplayName=@name,Biography=@bio
WHERE Id=@id AND Kind=0 AND Status=1",db);
        cmd.Parameters.AddWithValue("@name",data.DisplayName.Trim());
        cmd.Parameters.AddWithValue("@bio",(object?)data.Biography?.Trim()??DBNull.Value);
        cmd.Parameters.AddWithValue("@id",id);
        if(await cmd.ExecuteNonQueryAsync()<1)return null;
        await using var get=new SqlCommand(@"SELECT Email,Curp,Biography,DisplayName,CommunityRole
FROM dbo.Accounts WHERE Id=@id",db);
        get.Parameters.AddWithValue("@id",id);
        await using var r=await get.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        return new ApiUser(id,r.GetString(0),r.GetString(3),"Active","Community",
            r.IsDBNull(1)?null:r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),null,r.GetString(4));
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
