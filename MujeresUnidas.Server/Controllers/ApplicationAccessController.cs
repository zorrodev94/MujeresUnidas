using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Repositories;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers;

[ApiController]
[Route("api/management/access")]
public sealed class ApplicationAccessController(ApplicationAccessRepository access,AccountService accounts):ControllerBase
{
    private async Task<bool> OwnerAsync() => (await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString()))?.Kind=="Owner";
    [HttpGet]
    public async Task<IActionResult> All()=>await OwnerAsync()?Ok(await access.ListAsync()):StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
    [HttpPost("{id:guid}/community-professional")]
    public async Task<IActionResult> GrantCommunity(Guid id)
    {
        var actor=await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        if(actor?.Kind!="Owner")return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        return await access.GrantCommunityAsync(id,actor.Id)?Ok(new OperationResult("Acceso profesional de Comunidad autorizado. Perfil independiente disponible.")):
            BadRequest(new OperationResult("Requiere cuenta institucional activa y verificación simulada; o ya fue autorizada."));
    }
    [HttpPost("{id:guid}/{app}/suspend")]
    public async Task<IActionResult> Suspend(Guid id,string app)
    {
        var actor=await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        if(actor?.Kind!="Owner")return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        return await access.SuspendAsync(id,app,actor.Id)?Ok(new OperationResult("Acceso a la aplicación suspendido.")):
            BadRequest(new OperationResult("Acceso inexistente o no suspendible."));
    }
}
