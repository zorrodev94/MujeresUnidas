using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;
using MujeresUnidas.Shared.Contracts;

namespace MujeresUnidas.Server.Models.Analytics;

/// <summary>Persistence and privacy boundary for community self-reporting.</summary>
public sealed class CommunitySurveyRepository(Database database)
{
    private static readonly HashSet<string> Contributions = new(StringComparer.Ordinal)
        { "Yes", "Partial", "No", "PreferNot" };
    private static readonly HashSet<string> Resources = new(StringComparer.Ordinal)
        { "Jobs", "Studies", "Blogs", "Community", "None", "PreferNot" };

    public async Task<IReadOnlyList<CommunitySurveyInfo>> ActiveAsync()
    {
        var result = new List<CommunitySurveyInfo>();
        await using var db = await database.OpenAsync();
        await using var command = new SqlCommand(@"SELECT Id,Title,Description
FROM dbo.CommunitySurveys WHERE IsActive=1 ORDER BY CreatedUtc DESC", db);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public async Task<CommunitySurveyReceipt?> MineAsync(Guid surveyId, Guid accountId)
    {
        await using var db = await database.OpenAsync();
        await using var command = new SqlCommand(@"SELECT IsStudying,IsWorking,StudyStartedMonth,WorkStartedMonth,
Contribution,HelpfulResource,UpdatedUtc FROM dbo.CommunitySurveyResponses
WHERE SurveyId=@survey AND AccountId=@account", db);
        command.Parameters.Add("@survey", SqlDbType.UniqueIdentifier).Value = surveyId;
        command.Parameters.Add("@account", SqlDbType.UniqueIdentifier).Value = accountId;
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new(surveyId, reader.IsDBNull(0) ? null : reader.GetBoolean(0),
            reader.IsDBNull(1) ? null : reader.GetBoolean(1),
            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            reader.IsDBNull(3) ? null : reader.GetDateTime(3),
            reader.GetString(4), reader.GetString(5), reader.GetDateTime(6));
    }

    public async Task<bool> SubmitAsync(Guid surveyId, Guid accountId, CommunitySurveyAnswer answer)
    {
        if (!answer.Consent || !Contributions.Contains(answer.Contribution) ||
            !Resources.Contains(answer.HelpfulResource)) return false;
        if ((answer.IsStudying != true && answer.StudyStartedMonth.HasValue) ||
            (answer.IsWorking != true && answer.WorkStartedMonth.HasValue)) return false;
        if ((answer.StudyStartedMonth.HasValue && (answer.StudyStartedMonth.Value.Year < 2000 || answer.StudyStartedMonth.Value.Date > DateTime.UtcNow.Date)) ||
            (answer.WorkStartedMonth.HasValue && (answer.WorkStartedMonth.Value.Year < 2000 || answer.WorkStartedMonth.Value.Date > DateTime.UtcNow.Date))) return false;
        await using var db = await database.OpenAsync();
        await using var tx = (SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using (var command = new SqlCommand(@"IF NOT EXISTS(SELECT 1 FROM dbo.CommunitySurveys WHERE Id=@survey AND IsActive=1)
    SELECT CAST(0 AS int)
ELSE BEGIN
  MERGE dbo.CommunitySurveyResponses WITH(HOLDLOCK) AS target
  USING(SELECT @survey AS SurveyId, @account AS AccountId) AS source
  ON target.SurveyId=source.SurveyId AND target.AccountId=source.AccountId
  WHEN MATCHED THEN UPDATE SET IsStudying=@study, IsWorking=@work, StudyStartedMonth=@studyMonth,
       WorkStartedMonth=@workMonth, Contribution=@contribution, HelpfulResource=@resource,
       UpdatedUtc=SYSUTCDATETIME(),ConsentedUtc=SYSUTCDATETIME()
  WHEN NOT MATCHED THEN INSERT(SurveyId,AccountId,IsStudying,IsWorking,StudyStartedMonth,
       WorkStartedMonth,Contribution,HelpfulResource)
       VALUES(@survey,@account,@study,@work,@studyMonth,@workMonth,@contribution,@resource);
  SELECT CAST(1 AS int);
END", db, tx))
        {
            command.Parameters.Add("@survey", SqlDbType.UniqueIdentifier).Value = surveyId;
            command.Parameters.Add("@account", SqlDbType.UniqueIdentifier).Value = accountId;
            command.Parameters.Add("@study", SqlDbType.Bit).Value = (object?)answer.IsStudying ?? DBNull.Value;
            command.Parameters.Add("@work", SqlDbType.Bit).Value = (object?)answer.IsWorking ?? DBNull.Value;
            command.Parameters.Add("@studyMonth", SqlDbType.Date).Value = (object?)(answer.StudyStartedMonth is DateTime sm?new DateTime(sm.Year,sm.Month,1):(DateTime?)null) ?? DBNull.Value;
            command.Parameters.Add("@workMonth", SqlDbType.Date).Value = (object?)(answer.WorkStartedMonth is DateTime wm?new DateTime(wm.Year,wm.Month,1):(DateTime?)null) ?? DBNull.Value;
            command.Parameters.Add("@contribution", SqlDbType.NVarChar, 16).Value = answer.Contribution;
            command.Parameters.Add("@resource", SqlDbType.NVarChar, 16).Value = answer.HelpfulResource;
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 1)
            {
                await tx.RollbackAsync();
                return false;
            }
        }
        // Update the monthly observation in the same transaction, to keep the survey and goal source coherent.
        if (answer.IsStudying.HasValue || answer.IsWorking.HasValue)
        {
            await using var snapshot = new SqlCommand(@"MERGE dbo.CommunityOutcomeSnapshots WITH(HOLDLOCK) AS target
USING(SELECT @account AS AccountId,@year AS PeriodYear,@month AS PeriodMonth) AS source
ON target.AccountId=source.AccountId AND target.PeriodYear=source.PeriodYear AND target.PeriodMonth=source.PeriodMonth
WHEN MATCHED THEN UPDATE SET IsStudying=@study,IsWorking=@work,ConsentedUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(AccountId,PeriodYear,PeriodMonth,IsStudying,IsWorking)
VALUES(@account,@year,@month,@study,@work);", db, tx);
            snapshot.Parameters.Add("@account", SqlDbType.UniqueIdentifier).Value = accountId;
            snapshot.Parameters.Add("@year", SqlDbType.SmallInt).Value = (short)DateTime.UtcNow.Year;
            snapshot.Parameters.Add("@month", SqlDbType.TinyInt).Value = (byte)DateTime.UtcNow.Month;
            snapshot.Parameters.Add("@study", SqlDbType.Bit).Value = (object?)answer.IsStudying ?? DBNull.Value;
            snapshot.Parameters.Add("@work", SqlDbType.Bit).Value = (object?)answer.IsWorking ?? DBNull.Value;
            await snapshot.ExecuteNonQueryAsync();
        }
        else
        {
            await using var clear = new SqlCommand("DELETE FROM dbo.CommunityOutcomeSnapshots WHERE AccountId=@account AND PeriodYear=@year AND PeriodMonth=@month",db,tx);
            clear.Parameters.Add("@account",SqlDbType.UniqueIdentifier).Value=accountId;
            clear.Parameters.Add("@year",SqlDbType.SmallInt).Value=(short)DateTime.UtcNow.Year;
            clear.Parameters.Add("@month",SqlDbType.TinyInt).Value=(byte)DateTime.UtcNow.Month;
            await clear.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return true;
    }

    public async Task WithdrawAsync(Guid surveyId, Guid accountId)
    {
        await using var db = await database.OpenAsync();
        // Withdrawal covers both the individual survey and its aggregated source snapshots.
        // Never retain this consent-dependent data after the participant withdraws.
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var command = new SqlCommand(@"DELETE FROM dbo.CommunitySurveyResponses
WHERE SurveyId=@survey AND AccountId=@account;
IF @@ROWCOUNT > 0 DELETE FROM dbo.CommunityOutcomeSnapshots WHERE AccountId=@account;", db,tx);
        command.Parameters.Add("@survey", SqlDbType.UniqueIdentifier).Value = surveyId;
        command.Parameters.Add("@account", SqlDbType.UniqueIdentifier).Value = accountId;
        await command.ExecuteNonQueryAsync();
        await tx.CommitAsync();
    }

    /// <summary>Only aggregated annual counts; small cells are suppressed by the API.</summary>
    public async Task<CommunitySurveySummary?> SummaryAsync(int year)
    {
        if (year < 2020 || year > 2100) return null;
        await using var db = await database.OpenAsync();
        await using var command = new SqlCommand(@";WITH latest AS (SELECT *,ROW_NUMBER() OVER(PARTITION BY AccountId ORDER BY UpdatedUtc DESC,SurveyId) AS rn FROM dbo.CommunitySurveyResponses WHERE UpdatedUtc>=@from AND UpdatedUtc<@until)
SELECT COUNT(*),
 COALESCE(SUM(CASE WHEN IsStudying=1 THEN 1 ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN IsWorking=1 THEN 1 ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN IsStudying=1 AND IsWorking=1 THEN 1 ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN Contribution IN(N'Yes',N'Partial') THEN 1 ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN IsStudying=1 AND StudyStartedMonth>=@from AND StudyStartedMonth<@until THEN 1 ELSE 0 END),0),
 COALESCE(SUM(CASE WHEN IsWorking=1 AND WorkStartedMonth>=@from AND WorkStartedMonth<@until THEN 1 ELSE 0 END),0)
FROM latest WHERE rn=1", db);
        command.Parameters.Add("@from", SqlDbType.DateTime2).Value = new DateTime(year,1,1);
        command.Parameters.Add("@until", SqlDbType.DateTime2).Value = new DateTime(year+1,1,1);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var participants = reader.GetInt32(0);
        if (participants < 5) return null;
        int? Safe(int index){var n=reader.GetInt32(index);return n<5||participants-n<5?null:n;}
        return new(year,participants,Safe(1),Safe(2),Safe(3),Safe(4),Safe(5),Safe(6));
    }
}
