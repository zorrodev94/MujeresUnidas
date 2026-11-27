using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Badges;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers;

[ApiController]
[Route("api/badges")]
public sealed class BadgesController(BadgeRepository badges,AccountService accounts):ControllerBase
{
    private Task<ApiUser?> ActorAsync()=>accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
    [HttpGet]
    public async Task<IActionResult> List()=>await ActorAsync() is null?Unauthorized():Ok(await badges.ListAsync());
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Awards(Guid userId)
    {
        var actor=await ActorAsync();if(actor is null)return Unauthorized();
        // Public achievements are visible only to authenticated accounts; account lookup uses the ID, not secrets.
        var awarded=await badges.AwardsAsync(userId);
        return Ok(actor.Kind=="Community"
            ? awarded.Where(b=>b.Scope is "Global" or "Community").ToArray()
            : awarded);
    }
    public sealed record GrantRequest(Guid UserId,Guid BadgeId,string Reason);
    [HttpPost("grant")]
    public async Task<IActionResult> Grant([FromBody] GrantRequest data)
    {
        var actor=await ActorAsync();if(actor?.Kind!="Owner")return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        return await badges.AwardAsync(actor.Id,data.UserId,data.BadgeId,data.Reason)?
            Ok(new OperationResult("Reconocimiento registrado con auditoría.")):
            BadRequest(new OperationResult("No se pudo otorgar el reconocimiento."));
    }
}
