using System.Data;
using Microsoft.Data.SqlClient;
using MujeresUnidas.Server.Infrastructure.Database;

namespace MujeresUnidas.Server.Models.Reports
{
    /// <summary>Institutional metrics: requests, cases and completed appointments are distinct units.</summary>
    public sealed class CareAnalyticsRepository(Database database)
    {
        #region Contracts
        public sealed record MonthData(int Month,int Scheduled,int Completed);
        public sealed record SiteData(Guid SiteId,string SiteName,int CasesOpened,int Scheduled,int Completed);
        public sealed record YearData(int Year,int TargetCompletedSessions,int RequestsReceived,int CasesOpened,
            int CasesResolved,int AppointmentsScheduled,int SessionsCompleted,IReadOnlyList<MonthData> Months,
            IReadOnlyList<SiteData> Sites);
        public sealed record GoalData(int TargetCompletedSessions);
        #endregion

        #region Task
        public async Task<YearData> YearAsync(int year)
        {
            var start=new DateTime(year,1,1,0,0,0,DateTimeKind.Utc);
            var end=start.AddYears(1);
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"SELECT
 (SELECT COUNT(*) FROM dbo.CaseRequests WHERE CreatedUtc >= @start AND CreatedUtc < @end),
 (SELECT COUNT(*) FROM dbo.Cases WHERE OpenedUtc >= @start AND OpenedUtc < @end),
 (SELECT COUNT(*) FROM dbo.Cases WHERE ClosedUtc >= @start AND ClosedUtc < @end AND Status IN(N'Resolved',N'Closed')),
 (SELECT COUNT(*) FROM dbo.Appointments WHERE ScheduledUtc >= @start AND ScheduledUtc < @end),
 (SELECT COUNT(*) FROM dbo.AppointmentSessions WHERE EndedUtc >= @start AND EndedUtc < @end),
 (SELECT TargetCompletedSessions FROM dbo.AnnualCareGoals WHERE GoalYear=@year)",db);
            cmd.Parameters.Add("@start",SqlDbType.DateTime2).Value=start;
            cmd.Parameters.Add("@end",SqlDbType.DateTime2).Value=end;
            cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=year;
            int received,opened,resolved,scheduled,completed,goal;
            await using(var reader=await cmd.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                received=reader.GetInt32(0);opened=reader.GetInt32(1);resolved=reader.GetInt32(2);
                scheduled=reader.GetInt32(3);completed=reader.GetInt32(4);
                goal=reader.IsDBNull(5)?0:reader.GetInt32(5);
            }
            var byMonth=Enumerable.Range(1,12).Select(i=>new MonthData(i,0,0)).ToArray();
            await using var monthly=new SqlCommand(@"SELECT MONTH(ScheduledUtc),COUNT(*)
FROM dbo.Appointments WHERE ScheduledUtc>=@start AND ScheduledUtc<@end GROUP BY MONTH(ScheduledUtc)",db);
            monthly.Parameters.Add("@start",SqlDbType.DateTime2).Value=start;
            monthly.Parameters.Add("@end",SqlDbType.DateTime2).Value=end;
            await using(var rows=await monthly.ExecuteReaderAsync())
                while(await rows.ReadAsync())
                {
                    var m=rows.GetInt32(0);
                    byMonth[m-1]=byMonth[m-1] with { Scheduled=rows.GetInt32(1) };
                }
            await using var sessionMonthly=new SqlCommand(@"SELECT MONTH(EndedUtc),COUNT(*)
FROM dbo.AppointmentSessions WHERE EndedUtc>=@start AND EndedUtc<@end GROUP BY MONTH(EndedUtc)",db);
            sessionMonthly.Parameters.Add("@start",SqlDbType.DateTime2).Value=start;
            sessionMonthly.Parameters.Add("@end",SqlDbType.DateTime2).Value=end;
            await using(var rows=await sessionMonthly.ExecuteReaderAsync())
                while(await rows.ReadAsync())
                {
                    var m=rows.GetInt32(0);
                    byMonth[m-1]=byMonth[m-1] with { Completed=rows.GetInt32(1) };
                }
            var perSite=new List<SiteData>();
            await using var siteQuery=new SqlCommand(@"SELECT s.Id,s.DisplayName,
(SELECT COUNT(*) FROM dbo.Cases c WHERE c.SiteId=s.Id AND c.OpenedUtc>=@start AND c.OpenedUtc<@end),
(SELECT COUNT(*) FROM dbo.Appointments a WHERE a.SiteId=s.Id AND a.ScheduledUtc>=@start AND a.ScheduledUtc<@end),
(SELECT COUNT(*) FROM dbo.AppointmentSessions ses JOIN dbo.Appointments a ON a.Id=ses.AppointmentId WHERE a.SiteId=s.Id AND ses.EndedUtc>=@start AND ses.EndedUtc<@end)
FROM dbo.CareSites s ORDER BY s.Code",db);
            siteQuery.Parameters.Add("@start",SqlDbType.DateTime2).Value=start;
            siteQuery.Parameters.Add("@end",SqlDbType.DateTime2).Value=end;
            await using(var r=await siteQuery.ExecuteReaderAsync())
                while(await r.ReadAsync())perSite.Add(new SiteData(r.GetGuid(0),r.GetString(1),r.GetInt32(2),r.GetInt32(3),r.GetInt32(4)));
            return new YearData(year,goal,received,opened,resolved,scheduled,completed,byMonth,perSite);
        }

        public async Task<bool> UpdateGoalAsync(int year,int target,Guid actor)
        {
            if(year<2020||year>2100||target<0||target>1000000)return false;
            await using var db=await database.OpenAsync();
            await using var cmd=new SqlCommand(@"MERGE dbo.AnnualCareGoals WITH(HOLDLOCK) AS g
USING (SELECT @year AS GoalYear) AS incoming ON incoming.GoalYear=g.GoalYear
WHEN MATCHED THEN UPDATE SET TargetCompletedSessions=@target,UpdatedBy=@actor,UpdatedUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(GoalYear,TargetCompletedSessions,UpdatedBy) VALUES(@year,@target,@actor);",db);
            cmd.Parameters.Add("@year",SqlDbType.SmallInt).Value=year;
            cmd.Parameters.Add("@target",SqlDbType.Int).Value=target;
            cmd.Parameters.Add("@actor",SqlDbType.UniqueIdentifier).Value=actor;
            return await cmd.ExecuteNonQueryAsync()>0;
        }
        #endregion
    }
}
