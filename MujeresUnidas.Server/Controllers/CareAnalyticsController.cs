using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Reports;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Non-identifying operational analytics, not confidential clinical records.</summary>
    [ApiController]
    [Route("api/management/care-analytics")]
    public sealed class CareAnalyticsController(CareAnalyticsRepository analytics,AccountService accounts):ControllerBase
    {
        private Task<ApiUser?> ActorAsync()=>accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        private static bool Reviewer(ApiUser? a)=>a is {Kind:"Owner"} || a is {Kind:"Staff",Role:"Administrator"};
        [HttpGet("{year:int}")]
        public async Task<IActionResult> GetYear(int year)
        {
            if(!Reviewer(await ActorAsync()))return StatusCode(403);
            if(year is < 2020 or > 2099)return BadRequest(new OperationResult("Año no válido."));
            return Ok(await analytics.YearAsync(year));
        }
        [HttpPut("{year:int}/goal")]
        public async Task<IActionResult> UpdateGoal(int year,[FromBody] CareAnalyticsRepository.GoalData data)
        {
            var actor=await ActorAsync();
            if(actor?.Kind!="Owner")return StatusCode(403);
            return await analytics.UpdateGoalAsync(year,data.TargetCompletedSessions,actor.Id)?
                Ok(new OperationResult("Meta anual de demostración actualizada.")):
                BadRequest(new OperationResult("Meta inválida."));
        }
    }
}
