using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Care.Base;
using MujeresUnidas.Server.Models.Care.Repositories;
using MujeresUnidas.Server.Models.User.Services;
using CareCase=MujeresUnidas.Server.Models.Care.Base.Case;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Care requests and appointments. Resource access is verified from the login identity.</summary>
    [ApiController]
    [Route("api/care")]
    public sealed class CareController(CareRepository care,AccountService accounts) : ControllerBase
    {
        #region Task
        private Task<ApiUser?> ActorAsync()=>accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
        private static bool Reviewer(ApiUser? actor)=>actor?.Kind=="Owner" || (actor?.Kind=="Staff" && actor.Role=="Administrator");
        [HttpPost("requests")]
        public async Task<IActionResult> RequestCare([FromBody] CareCase.CreateRequestData input)
        {
            var actor=await ActorAsync();if(actor?.Kind!="Community")return StatusCode(403);
            var id=await care.RequestAsync(actor.Id,input);
            return id.HasValue?Ok(new {id=id.Value,message="Solicitud privada registrada. Gestión revisará su atención; no es un servicio de emergencias."}):BadRequest(new OperationResult("Revisa el área y el asunto general."));
        }
        [HttpGet("requests")]
        public async Task<IActionResult> Requests()
        {
            var actor=await ActorAsync();return actor is null?Unauthorized():Ok(await care.RequestsAsync(actor.Id,Reviewer(actor)));
        }
        [HttpPost("requests/{id:guid}/review")]
        public async Task<IActionResult> Review(Guid id,[FromBody] CareCase.ReviewData decision)
        {
            var actor=await ActorAsync();if(!Reviewer(actor))return StatusCode(403);
            var caseId=await care.ReviewAsync(id,actor!.Id,decision.Approve);
            return caseId.HasValue?Ok(new{caseId=caseId.Value,message="Revisión registrada."}):NotFound(new OperationResult("La solicitud ya fue revisada o no existe."));
        }
        [HttpGet("cases")]
        public async Task<IActionResult> Cases()
        {
            var actor=await ActorAsync();return actor is null?Unauthorized():Ok(await care.CasesAsync(actor.Id));
        }
        [HttpGet("management/cases")]
        public async Task<IActionResult> ManagementCases()
        {
            var actor=await ActorAsync();return Reviewer(actor)?Ok(await care.ManagementCasesAsync()):StatusCode(403);
        }
        [HttpGet("management/appointments")]
        public async Task<IActionResult> ManagementAppointments()
        {
            var actor=await ActorAsync();return Reviewer(actor)?Ok(await care.ManagementAppointmentsAsync()):StatusCode(403);
        }
        [HttpGet("management/professionals")]
        public async Task<IActionResult> Professionals()
        {
            var actor=await ActorAsync();
            if(!Reviewer(actor))return StatusCode(403);
            var people=await care.ProfessionalsAsync();
            return Ok(people.Select(p=>new{id=p.Id,displayName=p.DisplayName,role=p.Role}));
        }
        [HttpPost("cases/{id:guid}/assign")]
        public async Task<IActionResult> Assign(Guid id,[FromBody] CareCase.AssignmentData data)
        {
            var actor=await ActorAsync();if(!Reviewer(actor))return StatusCode(403);
            return await care.AssignAsync(id,data.ProfessionalId,actor!.Id)?Ok(new OperationResult("Profesional asignada.")):BadRequest(new OperationResult("La profesional no tiene el perfil y autorización necesarios."));
        }
        [HttpPut("cases/{id:guid}/status")]
        public async Task<IActionResult> UpdateCase(Guid id,[FromBody] CareCase.StatusData data)
        {
            var actor=await ActorAsync();if(actor is null)return Unauthorized();
            return await care.StatusAsync(id,actor.Id,data.Status)?Ok(new OperationResult("Caso actualizado.")):StatusCode(403,new OperationResult("Solo la profesional asignada puede cambiar este estado."));
        }
        [HttpPost("appointments")]
        public async Task<IActionResult> Schedule([FromBody] Appointment.ScheduleData data)
        {
            var actor=await ActorAsync();if(!Reviewer(actor))return StatusCode(403);
            var id=await care.ScheduleAsync(actor!.Id,data);
            return id.HasValue?Ok(new{id=id.Value,message="Cita programada."}):BadRequest(new OperationResult("Revisa la fecha futura, la sede asignada al caso y que la profesional esté vinculada a esa sede, asignada al caso, activa y autorizada para el área."));
        }
        [HttpGet("appointments")]
        public async Task<IActionResult> Appointments()
        {
            var actor=await ActorAsync();return actor is null?Unauthorized():Ok(await care.AppointmentsAsync(actor.Id));
        }
        [HttpPut("appointments/{id:guid}/status")]
        public async Task<IActionResult> UpdateAppointment(Guid id,[FromBody] Appointment.StatusData data)
        {
            var actor=await ActorAsync();if(actor is null)return Unauthorized();
            return await care.UpdateAppointmentAsync(id,actor.Id,data.Status,Reviewer(actor))?
                Ok(new OperationResult("Estado de cita actualizado.")):StatusCode(403,new OperationResult("Operación no autorizada o estado incompatible."));
        }
        [HttpPost("appointments/{id:guid}/session")]
        public async Task<IActionResult> CompleteSession(Guid id,[FromBody] Appointment.SessionData data)
        {
            var actor=await ActorAsync();if(actor is null)return Unauthorized();
            return await care.CompleteSessionAsync(id,actor.Id,data)
                ?Ok(new OperationResult("Sesión completada y contabilizada."))
                :StatusCode(403,new OperationResult("Solo la profesional asignada puede registrar una sesión confirmada."));
        }
        #endregion Task
    }
}
