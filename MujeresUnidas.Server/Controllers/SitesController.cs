using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Sites;
using MujeresUnidas.Server.Models.Sites.Repositories;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Institutional demo sites, with clear authorization for case and staff associations.</summary>
    [ApiController]
    [Route("api/sites")]
    public sealed class SitesController(CareSiteRepository sites,AccountService accounts):ControllerBase
    {
        #region Authorization
        private Task<ApiUser?> ActorAsync()=>accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        private static bool Manager(ApiUser? a)=>a is {Kind:"Owner"} || a is {Kind:"Staff",Role:"Administrator"};
        #endregion
        #region Task
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var actor=await ActorAsync();
            return actor is null?Unauthorized():Ok(await sites.ListAsync(!Manager(actor)));
        }
        [HttpGet("public")]
        public async Task<IActionResult> PublicCatalog()
        {
            // The public list contains only demonstrative site names, never invented street addresses.
            var rows=await sites.ListAsync();
            return Ok(rows.Select(s=>new{s.Id,s.Code,s.DisplayName,s.SiteType,s.Municipality,s.IsDemo}));
        }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SedeAtencion.CreateData data)
        {
            if(!Manager(await ActorAsync()))return StatusCode(403);
            return await sites.CreateAsync(data)?Ok(new OperationResult("Sede DEMO registrada.")):
                BadRequest(new OperationResult("Código duplicado o información inválida."));
        }
        [HttpPut("cases/{caseId:guid}/site")]
        public async Task<IActionResult> SetCaseSite(Guid caseId,[FromBody] SedeAtencion.CaseSiteData data)
        {
            var actor=await ActorAsync();
            if(!Manager(actor))return StatusCode(403);
            return await sites.AssignCaseAsync(caseId,data.SiteId,actor!.Id,data.Reason)?Ok(new OperationResult("Sede asignada al caso; traslado auditado.")):
                BadRequest(new OperationResult("Sede incompatible con el área, inactiva o caso con citas vigentes."));
        }
        [HttpPut("{siteId:guid}/zone")]
        public async Task<IActionResult> SetZone(Guid siteId,[FromBody] SedeAtencion.ZoneSiteData input)
        {
            if(!Manager(await ActorAsync()))return StatusCode(403);
            return await sites.SetZoneAsync(siteId,input.ZoneId)?Ok(new OperationResult("Sede vinculada a sector conceptual.")):
                BadRequest(new OperationResult("Sede o sector no disponible."));
        }
        [HttpPut("{siteId:guid}/staff")]
        public async Task<IActionResult> AssignStaff(Guid siteId,[FromBody] SedeAtencion.StaffSiteData data)
        {
            if(!Manager(await ActorAsync()))return StatusCode(403);
            return await sites.AssignStaffAsync(siteId,data.StaffId)?Ok(new OperationResult("Personal vinculado a la sede.")):
                BadRequest(new OperationResult("Sede o personal no disponible."));
        }
        #endregion
    }
}
