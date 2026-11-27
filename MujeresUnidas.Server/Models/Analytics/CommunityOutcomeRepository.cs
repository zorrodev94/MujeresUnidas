using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Analytics;

/// <summary>Voluntary, private self-report; never infers education or employment from posts.</summary>
public sealed record CommunityOutcomeInput(bool? IsStudying,bool? IsWorking,bool Consent);
public sealed record CommunityOutcomeView(int Year,int Month,bool? IsStudying,bool? IsWorking);
public sealed record CommunityOutcomeSummary(int Year,int Participants,int? Studying,int? Working,int? StudyingAndWorking);

public sealed class CommunityOutcomeRepository(Database database)
{
    public async Task<CommunityOutcomeView?> GetCurrentAsync(Guid accountId)
    {
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT TOP(1) PeriodYear,PeriodMonth,IsStudying,IsWorking
FROM dbo.CommunityOutcomeSnapshots WHERE AccountId=@id ORDER BY PeriodYear DESC,PeriodMonth DESC",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        return new(r.GetInt16(0),r.GetByte(1),r.IsDBNull(2)?null:r.GetBoolean(2),r.IsDBNull(3)?null:r.GetBoolean(3));
    }
    public async Task<bool> RecordAsync(Guid accountId,CommunityOutcomeInput input)
    {
        if(!input.Consent || (input.IsStudying is null && input.IsWorking is null))return false;
        var year=(short)DateTime.UtcNow.Year;
        var month=(byte)DateTime.UtcNow.Month;
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"MERGE dbo.CommunityOutcomeSnapshots WITH(HOLDLOCK) AS existing
USING(SELECT @id AccountId,@year PeriodYear,@month PeriodMonth) AS incoming
ON existing.AccountId=incoming.AccountId AND existing.PeriodYear=incoming.PeriodYear AND existing.PeriodMonth=incoming.PeriodMonth
WHEN MATCHED THEN UPDATE SET IsStudying=@study,IsWorking=@work,ConsentedUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(AccountId,PeriodYear,PeriodMonth,IsStudying,IsWorking)
VALUES(@id,@year,@month,@study,@work);",db);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
        cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=year;
        cmd.Parameters.Add("@month",SqlDbType.TinyInt).Value=month;
        cmd.Parameters.Add("@study",SqlDbType.Bit).Value=(object?)input.IsStudying??DBNull.Value;
        cmd.Parameters.Add("@work",SqlDbType.Bit).Value=(object?)input.IsWorking??DBNull.Value;
        return await cmd.ExecuteNonQueryAsync()>0;
    }
    public async Task<bool> WithdrawAsync(Guid accountId)
    {
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var cmd=new SqlCommand("DELETE FROM dbo.CommunitySurveyResponses WHERE AccountId=@id; DELETE FROM dbo.CommunityOutcomeSnapshots WHERE AccountId=@id;",db,tx);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=accountId;
        await cmd.ExecuteNonQueryAsync();await tx.CommitAsync();return true;
    }
    public async Task<CommunityOutcomeSummary?> SummaryAsync(int year)
    {
        if(year<2020||year>2100)return null;
        await using var db=await database.OpenAsync();
        // Latest voluntary observation per account in this year. No individual data leaves the server.
        await using var cmd=new SqlCommand(@";WITH latest AS
(SELECT AccountId,IsStudying,IsWorking,ROW_NUMBER() OVER(PARTITION BY AccountId ORDER BY PeriodMonth DESC) rn
 FROM dbo.CommunityOutcomeSnapshots WHERE PeriodYear=@year)
SELECT COUNT(*),
 COALESCE(SUM(CASE WHEN IsStudying=1 THEN 1 ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN IsWorking=1 THEN 1 ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN IsStudying=1 AND IsWorking=1 THEN 1 ELSE 0 END),0)
FROM latest WHERE rn=1",db);
        cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=(short)year;
        await using var r=await cmd.ExecuteReaderAsync();
        if(!await r.ReadAsync())return null;
        int count=r.GetInt32(0);
        // Suppress small counts that might identify someone in a demo site or group.
        if(count<5)return null;
        int? Safe(int index){int n=r.GetInt32(index);return n<5||count-n<5?null:n;}
        return new(year,count,Safe(1),Safe(2),Safe(3));
    }
}
