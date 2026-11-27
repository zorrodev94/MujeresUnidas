using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Safety;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Community help preferences and non-emergency reports. All contact reads/writes are owner-scoped.</summary>
    [ApiController]
    [Route("api/safety")]
    public sealed class SafetyController(AccountService accounts, EmergencyContactRepository contacts,
        IncidentRepository incidents) : ControllerBase
    {
        #region Authorization
        private Task<ApiUser?> ActorAsync() => accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
        private static bool Reviewer(ApiUser? actor) => actor is { Kind: "Owner" } ||
            actor is { Kind: "Staff", Role: "Administrator" };
        #endregion

        #region Emergency Contacts
        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts()
        {
            var actor = await ActorAsync();
            if (actor is null) return Unauthorized();
            if (actor.Kind != "Community") return StatusCode(403);
            if (!contacts.IsConfigured) return StatusCode(503, new OperationResult("Configura MU_CONTACT_KEY_BASE64 en Server para guardar contactos cifrados."));
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(await contacts.AllAsync(actor.Id));
        }

        [HttpPost("contacts")]
        public async Task<IActionResult> AddContact([FromBody] EmergencyContactRepository.CreateContactData data)
        {
            var actor = await ActorAsync();
            if (actor is null) return Unauthorized();
            if (actor.Kind != "Community") return StatusCode(403);
            if (!contacts.IsConfigured) return StatusCode(503, new OperationResult("El cifrado de contactos no está configurado en Server."));
            var result = await contacts.AddAsync(actor.Id, data);
            return result.Saved ? Ok(new OperationResult(result.Message)) : BadRequest(new OperationResult(result.Message));
        }

        [HttpDelete("contacts/{id:guid}")]
        public async Task<IActionResult> DeleteContact(Guid id)
        {
            var actor = await ActorAsync();
            if (actor is null) return Unauthorized();
            if (actor.Kind != "Community") return StatusCode(403);
            return await contacts.DeleteAsync(actor.Id, id) ? NoContent() : NotFound();
        }
        #endregion

        #region Incident Reports
        [HttpGet("incidents")]
        public async Task<IActionResult> MyIncidents()
        {
            var actor = await ActorAsync();
            if (actor is null) return Unauthorized();
            if (actor.Kind != "Community") return StatusCode(403);
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(await incidents.MineAsync(actor.Id));
        }

        [HttpPost("incidents")]
        public async Task<IActionResult> CreateIncident([FromBody] IncidentRepository.CreateIncidentData data)
        {
            var actor = await ActorAsync();
            if (actor is null) return Unauthorized();
            if (actor.Kind != "Community") return StatusCode(403);
            var id = await incidents.AddAsync(actor.Id, data);
            return id is null ? BadRequest(new OperationResult("Selecciona una categoría y escribe un resumen general de 15 a 900 caracteres.")) :
                Ok(new { id, message = "Registro de demostración creado. No se notificó a servicios de emergencia." });
        }

        [HttpGet("management/incidents")]
        public async Task<IActionResult> ManagementIncidents()
        {
            if (!Reviewer(await ActorAsync())) return StatusCode(403);
            Response.Headers["Cache-Control"] = "no-store";
            return Ok(await incidents.ManagementListAsync());
        }

        [HttpPut("management/incidents/{id:guid}/status")]
        public async Task<IActionResult> UpdateIncident(Guid id, [FromBody] IncidentRepository.StatusData data)
        {
            if (!Reviewer(await ActorAsync())) return StatusCode(403);
            return await incidents.UpdateStatusAsync(id, data.Status) ? Ok(new OperationResult("Estado actualizado.")) :
                BadRequest(new OperationResult("Estado inválido o registro inexistente."));
        }
        #endregion
    }
}
