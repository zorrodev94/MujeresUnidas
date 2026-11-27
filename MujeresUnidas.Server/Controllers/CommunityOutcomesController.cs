using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Analytics;
using MujeresUnidas.Server.Models.User.Services;
namespace MujeresUnidas.Server.Controllers;

/// <summary>Voluntary studies/work status. Only aggregate institutional stats leave the private store.</summary>
[ApiController]
[Route("api/outcomes")]
public sealed class CommunityOutcomesController(CommunityOutcomeRepository outcomes,AccountService accounts):ControllerBase
{
    private Task<ApiUser?> ActorAsync()=>accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
    [HttpGet("me")]
    public async Task<IActionResult> Mine()
    {
        var actor=await ActorAsync();return actor?.Kind=="Community"?
            Ok(await outcomes.GetCurrentAsync(actor.Id)):Unauthorized();
    }
    [HttpPut("me")]
    public async Task<IActionResult> Update([FromBody] CommunityOutcomeInput input)
    {
        var actor=await ActorAsync();if(actor?.Kind!="Community")return Unauthorized();
        return await outcomes.RecordAsync(actor.Id,input)?
            Ok(new OperationResult("Información voluntaria guardada.")):
            BadRequest(new OperationResult("Confirma consentimiento y elige una situación para guardar."));
    }
    [HttpDelete("me")]
    public async Task<IActionResult> Withdraw()
    {
        var actor=await ActorAsync();return actor?.Kind=="Community" && await outcomes.WithdrawAsync(actor.Id)?
            Ok(new OperationResult("Tu información voluntaria fue retirada.")):Unauthorized();
    }
    [HttpGet("summary/{year:int}")]
    public async Task<IActionResult> Summary(int year)
    {
        var actor=await ActorAsync();
        if(actor is not {Kind:"Owner"} && actor is not {Kind:"Staff",Role:"Administrator"})return StatusCode(403);
        var aggregate=await outcomes.SummaryAsync(year);
        return aggregate is null?Ok(new{year,available=false,note="Muestra insuficiente (mínimo 5 participantes)."}):
            Ok(new {available=true,data=aggregate});
    }
}
