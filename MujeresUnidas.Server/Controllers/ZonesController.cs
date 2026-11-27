using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Geography;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers;

[ApiController]
[Route("api/management/zones")]
public sealed class ZonesController(ZoneRepository zones,AccountService accounts):ControllerBase
{
    private async Task<bool> AllowedAsync()
    {
        var actor=await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        return actor is {Kind:"Owner"} || actor is {Kind:"Staff",Role:"Administrator"};
    }
    [HttpGet]
    public async Task<IActionResult> List()=>await AllowedAsync()?Ok(await zones.GetZonesAsync()):StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
    [HttpPut("{id:guid}/geometry")]
    public async Task<IActionResult> Save(Guid id,[FromBody] ZoneGeometryUpdate request)
    {
        if(!await AllowedAsync())return StatusCode(403,new OperationResult("No tienes autorización para esta operación."));
        return await zones.UpdateGeometryAsync(id,request)?Ok(new OperationResult("Geometría de demostración registrada.")):
            BadRequest(new OperationResult("GeoJSON inválido o zona no encontrada."));
    }
}
