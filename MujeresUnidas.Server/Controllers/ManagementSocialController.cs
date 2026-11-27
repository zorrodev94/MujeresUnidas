using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Publications.Management;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers;

[ApiController]
[Route("api/management/social")]
public sealed class ManagementSocialController(ManagementSocialRepository social,AccountService accounts):ControllerBase
{
    private async Task<ApiUser?> ActorAsync()
    {
        var actor=await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        return actor?.Kind is "Owner" or "Staff"?actor:null;
    }
    [HttpGet("moments")]
    public async Task<IActionResult> Moments([FromQuery] bool saved=false)
    {
        var actor=await ActorAsync();return actor is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):Ok(await social.MomentsAsync(actor.Id,saved));
    }
    [HttpPost("moments")]
    public async Task<IActionResult> Create([FromBody] MomentCreate data)
    {
        var actor=await ActorAsync();if(actor is null)return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        var id=await social.CreateAsync(actor.Id,data.Body);
        return id is null?BadRequest(new OperationResult("Escribe un Momento de 1 a 1200 caracteres.")):Ok(new{id});
    }
    [HttpPut("moments/{id:guid}/like")]
    public async Task<IActionResult> Like(Guid id)=>await SetLike(id,true);
    [HttpDelete("moments/{id:guid}/like")]
    public async Task<IActionResult> Unlike(Guid id)=>await SetLike(id,false);
    private async Task<IActionResult> SetLike(Guid id,bool value)
    {
        var actor=await ActorAsync();return actor is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):
            await social.SetLikeAsync(actor.Id,id,value)?Ok(new OperationResult("Reacción guardada.")):NotFound();
    }
    [HttpPut("moments/{id:guid}/save")]
    public async Task<IActionResult> Save(Guid id)=>await SetSave(id,true);
    [HttpDelete("moments/{id:guid}/save")]
    public async Task<IActionResult> Unsave(Guid id)=>await SetSave(id,false);
    private async Task<IActionResult> SetSave(Guid id,bool value)
    {
        var actor=await ActorAsync();return actor is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):
            await social.SetSavedAsync(actor.Id,id,value)?Ok(new OperationResult("Guardado actualizado.")):NotFound();
    }
    [HttpGet("moments/{id:guid}/comments")]
    public async Task<IActionResult> Comments(Guid id)=>await ActorAsync() is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):Ok(await social.CommentsAsync(id));
    [HttpPost("moments/{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id,[FromBody] CommentCreate input)
    {
        var actor=await ActorAsync();return actor is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):
            await social.CommentAsync(actor.Id,id,input.Body)?Ok(new OperationResult("Comentario enviado.")):
                BadRequest(new OperationResult("No se pudo registrar el comentario."));
    }
    [HttpDelete("moments/{id:guid}")]
    public async Task<IActionResult> Hide(Guid id)
    {
        var actor=await ActorAsync();return actor is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):
            await social.HideAsync(actor.Id,id,actor.Kind=="Owner"||actor.Role=="Administrator")?
                Ok(new OperationResult("Momento ocultado.")):NotFound();
    }
    [HttpGet("profiles")]
    public async Task<IActionResult> Profiles([FromQuery] string? q="")
    {
        var actor=await ActorAsync();if(actor is null)return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        var term=(q??string.Empty).Trim();if(term.Length>80)return BadRequest();
        return Ok(await social.SearchAsync(actor.Id,term));
    }
    [HttpPost("profiles/{id:guid}/follow")]
    public async Task<IActionResult> Follow(Guid id)=>await Toggle(id,true);
    [HttpDelete("profiles/{id:guid}/follow")]
    public async Task<IActionResult> Unfollow(Guid id)=>await Toggle(id,false);
    private async Task<IActionResult> Toggle(Guid id,bool follow)
    {
        var actor=await ActorAsync();return actor is null?StatusCode(403,new OperationResult("No tienes autorización para esta operación.")):
            await social.FollowAsync(actor.Id,id,follow)?Ok(new OperationResult("Relación actualizada.")):NotFound();
    }
}
