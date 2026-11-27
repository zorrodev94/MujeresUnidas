using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Messages;
using MujeresUnidas.Server.Models.Messages.Management;
using MujeresUnidas.Server.Models.Messages.Repositories;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Staff-only messaging. Community accounts cannot access any of these endpoints.</summary>
    [ApiController]
    [Route("api/management/messaging")]
    public sealed class ManagementMessagesController(
        ManagementConversationRepository conversations,AccountService accounts,MessageSecurity security) : ControllerBase
    {
        #region Operation Data
        #endregion Operation Data
        #region Initialization
        #endregion Initialization
        #region Arrays
        #endregion Arrays
        #region Structs
        #endregion Structs
        #region Task
        private async Task<ApiUser?> StaffAsync()
        {
            var user=await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
            return user?.Kind is "Owner" or "Staff" ? user : null;
        }
        [HttpGet("staff")]
        public async Task<IActionResult> Staff()
        {
            var actor=await StaffAsync();
            return actor is null?StatusCode(403,new OperationResult("Solo personal institucional autorizado.")):
                Ok(await conversations.StaffAsync(actor.Id));
        }
        [HttpGet("conversations")]
        public async Task<IActionResult> List()
        {
            var actor=await StaffAsync();
            return actor is null?StatusCode(403,new OperationResult("Solo personal institucional autorizado.")):
                Ok(await conversations.ListAsync(actor.Id));
        }
        [HttpPost("conversations")]
        public async Task<IActionResult> Start([FromBody] ManagementConversation.StartData input)
        {
            var actor=await StaffAsync();
            if(actor is null)return StatusCode(403,new OperationResult("Solo personal institucional autorizado."));
            var id=await conversations.StartAsync(actor.Id,input.RecipientId);
            return id.HasValue?Ok(new {conversationId=id.Value}):
                BadRequest(new OperationResult("El destinatario debe ser personal activo distinto de tu cuenta."));
        }
        [HttpGet("conversations/{id:guid}/messages")]
        public async Task<IActionResult> Messages(Guid id)
        {
            var actor=await StaffAsync();
            if(actor is null)return StatusCode(403,new OperationResult("Solo personal institucional autorizado."));
            if(!security.IsConfigured)return KeyUnavailable();
            try
            {
                var result=await conversations.MessagesAsync(id,actor.Id);
                return result is null?NotFound(new OperationResult("Conversación no disponible.")):Ok(result);
            }
            catch(CryptographicException)
            {
                return StatusCode(503,new OperationResult("La clave original de mensajes no descifra estos registros."));
            }
        }
        [HttpPost("conversations/{id:guid}/messages")]
        public async Task<IActionResult> Send(Guid id,[FromBody] ManagementConversation.SendData input)
        {
            var actor=await StaffAsync();
            if(actor is null)return StatusCode(403,new OperationResult("Solo personal institucional autorizado."));
            string? body=input.Body?.Trim();
            if(string.IsNullOrWhiteSpace(body)||body.Length>2000)
                return BadRequest(new OperationResult("El mensaje debe tener de 1 a 2000 caracteres."));
            if(!security.IsConfigured)return KeyUnavailable();
            var result=await conversations.SendAsync(id,actor.Id,body);
            return result.HasValue?Ok(new {messageId=result.Value}):
                NotFound(new OperationResult("Conversación no disponible para tu cuenta."));
        }
        private IActionResult KeyUnavailable()=>StatusCode(503,new OperationResult(
            "Configura MU_MESSAGE_KEY_BASE64 en secretos del servidor, conservando la clave original."));
        #endregion Task
    }
}
