using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Shared.Contracts;
using MujeresUnidas.Server.Models.Analytics;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers;

/// <summary>Public survey invitation, private answers, and suppressed institutional aggregates.</summary>
[ApiController]
[Route("api/community/surveys")]
public sealed class CommunitySurveysController(
    CommunitySurveyRepository surveys,AccountService accounts):ControllerBase
{
    private Task<ApiUser?> ActorAsync()=>accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());

    [HttpGet("active")]
    public async Task<IActionResult> Active()
    {
        if ((await ActorAsync())?.Kind!="Community") return Unauthorized();
        return Ok(await surveys.ActiveAsync());
    }

    [HttpGet("{surveyId:guid}/mine")]
    public async Task<IActionResult> Mine(Guid surveyId)
    {
        var actor=await ActorAsync();if(actor?.Kind!="Community")return Unauthorized();
        return Ok(await surveys.MineAsync(surveyId,actor.Id));
    }

    [HttpPut("{surveyId:guid}/response")]
    public async Task<IActionResult> Submit(Guid surveyId,[FromBody] CommunitySurveyAnswer answer)
    {
        var actor=await ActorAsync();if(actor?.Kind!="Community")return Unauthorized();
        return await surveys.SubmitAsync(surveyId,actor.Id,answer)
            ? Ok(new OperationResult("Encuesta guardada. Tu respuesta individual es privada."))
            : BadRequest(new OperationResult("Revisa el consentimiento, las fechas y las respuestas."));
    }

    [HttpDelete("{surveyId:guid}/response")]
    public async Task<IActionResult> Withdraw(Guid surveyId)
    {
        var actor=await ActorAsync();if(actor?.Kind!="Community")return Unauthorized();
        await surveys.WithdrawAsync(surveyId,actor.Id);
        return Ok(new OperationResult("Respuesta de la encuesta retirada."));
    }

    [HttpGet("management/summary/{year:int}")]
    public async Task<IActionResult> Summary(int year)
    {
        var actor=await ActorAsync();
        if(actor is not {Kind:"Owner"} && actor is not {Kind:"Staff",Role:"Administrator"})return StatusCode(403);
        var result=await surveys.SummaryAsync(year);
        return result is null
            ? Ok(new {available=false,note="No hay suficientes respuestas para mostrar resultados agregados (mínimo 5)."})
            : Ok(new {available=true,data=result});
    }
}
