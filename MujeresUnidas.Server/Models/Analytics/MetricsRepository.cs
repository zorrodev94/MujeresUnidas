using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Analytics;

/// <summary>All results are institutional indicators. Never equate recorded reports with actual prevalence.</summary>
public sealed class MetricsRepository(Database database):IGoalManager
{
    public async Task<IReadOnlyList<MetricDefinition>> GetDefinitionsAsync()
    {
        var result=new List<MetricDefinition>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand("SELECT Id,Code,Title,Unit,Direction,DataSource FROM dbo.MetricDefinitions WHERE IsActive=1 ORDER BY Title",db);
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())result.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),Enum.Parse<GoalDirection>(r.GetString(4)),r.GetString(5)));
        return result;
    }
    public async Task<IReadOnlyList<MetricGoal>> GetGoalsAsync(int year)
    {
        var list=new List<MetricGoal>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand("SELECT Id,MetricId,GoalYear,ZoneId,SiteId,TargetValue,Status,Notes FROM dbo.MetricGoals WHERE GoalYear=@year ORDER BY CreatedUtc DESC",db);
        cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=year;
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())list.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetInt16(2),r.IsDBNull(3)?null:r.GetGuid(3),r.IsDBNull(4)?null:r.GetGuid(4),r.GetDecimal(5),Enum.Parse<GoalStatus>(r.GetString(6)),r.IsDBNull(7)?null:r.GetString(7)));
        return list;
    }
    public async Task<Guid?> CreateGoalAsync(GoalCreateRequest input,Guid actorId)
    {
        if(input.MetricId==Guid.Empty||input.GoalYear is <2020 or >2100||input.TargetValue<0||input.TargetValue>1_000_000_000||
            (input.SiteId is not null && input.ZoneId is not null)||(input.Notes?.Length??0)>400)return null;
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"IF NOT EXISTS(SELECT 1 FROM dbo.MetricDefinitions WHERE Id=@metric AND IsActive=1
 AND (Direction=N'Decrease' OR @target>0))
 SELECT CAST(NULL AS uniqueidentifier)
ELSE BEGIN
 DECLARE @id uniqueidentifier=NEWID();
 INSERT dbo.MetricGoals(Id,MetricId,GoalYear,ZoneId,SiteId,TargetValue,CreatedBy,Notes)
 VALUES(@id,@metric,@year,@zone,@site,@target,@actor,@notes);
 SELECT @id;
END",db);
        cmd.Parameters.Add("@metric",SqlDbType.UniqueIdentifier).Value=input.MetricId;
        cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=input.GoalYear;
        cmd.Parameters.Add("@zone",SqlDbType.UniqueIdentifier).Value=(object?)input.ZoneId??DBNull.Value;
        cmd.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=(object?)input.SiteId??DBNull.Value;
        cmd.Parameters.Add("@target",SqlDbType.Decimal).Value=input.TargetValue;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
        cmd.Parameters.Add("@notes",SqlDbType.NVarChar,400).Value=(object?)input.Notes??DBNull.Value;
        var result=await cmd.ExecuteScalarAsync();return result is Guid id?id:null;
    }
    public async Task<bool> EvaluateGoalAsync(Guid goalId,GoalEvaluationRequest input,Guid actorId)
    {
        if(goalId==Guid.Empty||input.ObservedValue<0||input.ObservedValue>1_000_000_000||
           string.IsNullOrWhiteSpace(input.Explanation) || input.Explanation.Trim().Length<8 ||
           input.Explanation.Length>400||input.Status is GoalStatus.Planned)return false;
        await using var db=await database.OpenAsync();
        await using var tx=(SqlTransaction)await db.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var current=new SqlCommand(@"SELECT g.TargetValue,d.Direction FROM dbo.MetricGoals g WITH(UPDLOCK,HOLDLOCK)
JOIN dbo.MetricDefinitions d ON d.Id=g.MetricId WHERE g.Id=@id",db,tx);
        current.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=goalId;
        decimal target;
        GoalDirection direction;
        await using(var reader=await current.ExecuteReaderAsync())
        {
            if(!await reader.ReadAsync()){await tx.RollbackAsync();return false;}
            target=reader.GetDecimal(0);
            direction=Enum.Parse<GoalDirection>(reader.GetString(1));
        }
        bool achieved=direction switch
        {
            GoalDirection.Increase=>target>0 && input.ObservedValue>=target,
            GoalDirection.Decrease=>input.ObservedValue<=target,
            _=>false
        };
        if(input.Status==GoalStatus.Achieved && !achieved)
        {
            await tx.RollbackAsync();return false;
        }
        var finalStatus=achieved && input.Status is not GoalStatus.Rescheduled ? GoalStatus.Achieved : input.Status;
        await using var cmd=new SqlCommand(@"UPDATE dbo.MetricGoals SET Status=@status,UpdatedUtc=SYSUTCDATETIME() WHERE Id=@id;
IF @@ROWCOUNT=1 BEGIN
 INSERT dbo.GoalEvaluations(GoalId,ObservedValue,Status,Explanation,EvaluatedBy)
 VALUES(@id,@value,@status,@explanation,@actor);
 SELECT 1;
END ELSE SELECT 0;",db,tx);
        cmd.Parameters.Add("@id",SqlDbType.UniqueIdentifier).Value=goalId;
        cmd.Parameters.Add("@status",SqlDbType.NVarChar,16).Value=finalStatus.ToString();
        cmd.Parameters.Add("@value",SqlDbType.Decimal).Value=input.ObservedValue;
        cmd.Parameters.Add("@explanation",SqlDbType.NVarChar,400).Value=(object?)input.Explanation??DBNull.Value;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actorId;
        var ok=Convert.ToInt32(await cmd.ExecuteScalarAsync())==1;
        await tx.CommitAsync();return ok;
    }
    public async Task<bool> RecordObservationAsync(MetricObservation item,Guid actor)
    {
        if(item.MetricId==Guid.Empty||item.Year is <2020 or >2100||item.Month is <1 or >12||item.ObservedValue<0||
          string.IsNullOrWhiteSpace(item.SourceNote)||item.SourceNote.Length>300||
          (item.ZoneId is not null&&item.SiteId is not null))return false;
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"INSERT dbo.MetricObservations(MetricId,PeriodYear,PeriodMonth,ZoneId,SiteId,ObservedValue,SourceNote,IsDemonstration,RecordedBy)
VALUES(@metric,@year,@month,@zone,@site,@value,@source,@demo,@actor)",db);
        cmd.Parameters.Add("@metric",SqlDbType.UniqueIdentifier).Value=item.MetricId;
        cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=item.Year;
        cmd.Parameters.Add("@month",SqlDbType.TinyInt).Value=item.Month;
        cmd.Parameters.Add("@zone",SqlDbType.UniqueIdentifier).Value=(object?)item.ZoneId??DBNull.Value;
        cmd.Parameters.Add("@site",SqlDbType.UniqueIdentifier).Value=(object?)item.SiteId??DBNull.Value;
        cmd.Parameters.Add("@value",SqlDbType.Decimal).Value=item.ObservedValue;
        cmd.Parameters.Add("@source",SqlDbType.NVarChar,300).Value=item.SourceNote.Trim();
        cmd.Parameters.Add("@demo",SqlDbType.Bit).Value=item.IsDemonstration;
        cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
        return await cmd.ExecuteNonQueryAsync()==1;
    }
    public async Task<IReadOnlyList<MetricObservation>> GetObservationsAsync(int year)
    {
        var result=new List<MetricObservation>();
        await using var db=await database.OpenAsync();
        await using var cmd=new SqlCommand(@"SELECT MetricId,PeriodYear,PeriodMonth,ObservedValue,SourceNote,IsDemonstration,ZoneId,SiteId
FROM dbo.MetricObservations WHERE PeriodYear=@year ORDER BY PeriodMonth,RecordedUtc",db);
        cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=year;
        await using var r=await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync())result.Add(new(r.GetGuid(0),r.GetInt16(1),r.GetByte(2),r.GetDecimal(3),r.GetString(4),r.GetBoolean(5),r.IsDBNull(6)?null:r.GetGuid(6),r.IsDBNull(7)?null:r.GetGuid(7)));
        return result;
    }
}
