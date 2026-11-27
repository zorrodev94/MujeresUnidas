using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Models.Analytics;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Contracts;

namespace MujeresUnidas.Server.Controllers;

[ApiController]
[Route("api/management/goals")]
public sealed class GoalsController(MetricsRepository metrics,AccountService accounts):ControllerBase
{
    private async Task<ApiUser?> AdminAsync()
    {
        var actor=await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        return actor is {Kind:"Owner"} || actor is {Kind:"Staff",Role:"Administrator"}?actor:null;
    }
    [HttpGet("definitions")]
    public async Task<IActionResult> Definitions()=>await AdminAsync() is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):Ok(await metrics.GetDefinitionsAsync());
    [HttpGet("{year:int}")]
    public async Task<IActionResult> List(int year)=>await AdminAsync() is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):Ok(new{goals=await metrics.GetGoalsAsync(year),observations=await metrics.GetObservationsAsync(year)});
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GoalCreateRequest data)
    {
        var actor=await AdminAsync();if(actor is null)return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        var id=await metrics.CreateGoalAsync(data,actor.Id);
        return id is null?BadRequest(new OperationResult("Revisa indicador, período y ámbito de la meta.")):Ok(new{id});
    }
    [HttpPost("{id:guid}/evaluate")]
    public async Task<IActionResult> Evaluate(Guid id,[FromBody] GoalEvaluationRequest data)
    {
        var actor=await AdminAsync();if(actor is null)return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        return await metrics.EvaluateGoalAsync(id,data,actor.Id)?Ok(new OperationResult("Evaluación registrada.")):
            BadRequest(new OperationResult("No se pudo registrar la evaluación."));
    }
    [HttpPost("observations")]
    public async Task<IActionResult> Observe([FromBody] MetricObservation data)
    {
        var actor=await AdminAsync();if(actor is null)return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        return await metrics.RecordObservationAsync(data,actor.Id)?Ok(new OperationResult("Observación registrada.")):
            BadRequest(new OperationResult("Observación inválida."));
    }
}
