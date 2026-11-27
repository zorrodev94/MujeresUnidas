using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Messages;
using MujeresUnidas.Server.Models.Messages.Repositories;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.Media;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Community messaging; every operation verifies a server-side session and membership.</summary>
    [ApiController]
    [Route("api/conversations")]
    public sealed class MessagesController(ConversationRepository conversations,AccountService accounts,MessageSecurity security,MessageAttachmentRepository media) : ControllerBase
    {
        #region Task
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var actor=await CommunityAsync();
            return actor is null?Unauthorized():Ok(await conversations.ListAsync(actor.Id));
        }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Conversation.CreateData data)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            if(string.IsNullOrWhiteSpace(data.Title)||data.Title.Trim().Length is < 2 or > 100)
                return BadRequest(new OperationResult("Nombre de conversación inválido."));
            Guid? id=await conversations.CreateAsync(actor.Id,data,actor.CommunityRole=="Staff");
            return id is null?BadRequest(new OperationResult("Participantes inválidos o permiso insuficiente.")):
                Ok(new {conversationId=id.Value});
        }
        [HttpGet("{id:guid}/messages")]
        public async Task<IActionResult> Messages(Guid id)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            if(!security.IsConfigured)return EncryptionUnavailable();
            try
            {
                var rows=await conversations.MessagesAsync(id,actor.Id);
                return rows is null?NotFound(new OperationResult("Conversación no disponible.")):Ok(rows);
            }
            catch(CryptographicException)
            {return StatusCode(503,new OperationResult("No se pudieron descifrar mensajes anteriores. Restaura la clave original del servidor."));}
        }
        [HttpPost("{id:guid}/messages")]
        public async Task<IActionResult> Send(Guid id,[FromBody] Message.SendData data)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            var body=data.Body?.Trim();
            if(string.IsNullOrEmpty(body)||body.Length>2000)return BadRequest(new OperationResult("El mensaje debe tener de 1 a 2000 caracteres."));
            if(!security.IsConfigured)return EncryptionUnavailable();
            Guid? messageId=await conversations.SendAsync(id,actor.Id,body);
            return messageId is null?NotFound(new OperationResult("Conversación no disponible.")):
                Ok(new {messageId=messageId.Value});
        }
        [HttpPut("messages/{messageId:guid}")]
        public async Task<IActionResult> Edit(Guid messageId,[FromBody] Message.EditData data)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            var body=data.Body?.Trim();
            if(string.IsNullOrEmpty(body)||body.Length>2000)return BadRequest(new OperationResult("Mensaje inválido."));
            if(!security.IsConfigured)return EncryptionUnavailable();
            return await conversations.EditAsync(messageId,actor.Id,body)?Ok(new OperationResult("Mensaje editado.")):NotFound();
        }
        [HttpDelete("messages/{messageId:guid}")]
        public async Task<IActionResult> Delete(Guid messageId)
        {
            var actor=await CommunityAsync();
            return actor is null?Unauthorized():await conversations.DeleteAsync(messageId,actor.Id)?
                Ok(new OperationResult("Mensaje eliminado.")):NotFound();
        }
        [HttpPost("{id:guid}/attachments")]
        [RequestSizeLimit(16*1024*1024)]
        public async Task<IActionResult> Upload(Guid id,IFormFile file)
        {
            var actor=await CommunityAsync();if(actor is null)return Unauthorized();
            var result=await media.UploadAsync(id,actor.Id,file);
            return result.MessageId is Guid messageId?Ok(new{messageId}):
                BadRequest(new OperationResult(result.Error));
        }
        [HttpGet("{id:guid}/attachments")]
        public async Task<IActionResult> Attachments(Guid id)
        {
            var actor=await CommunityAsync();if(actor is null)return Unauthorized();
            var items=await media.ListAsync(id,actor.Id);
            return items is null?NotFound():Ok(items);
        }
        [HttpGet("{id:guid}/attachments/{assetId:guid}")]
        public async Task<IActionResult> Download(Guid id,Guid assetId)
        {
            var actor=await CommunityAsync();if(actor is null)return Unauthorized();
            if(!security.IsConfigured)return EncryptionUnavailable();
            try
            {
                var item=await media.GetAsync(id,assetId,actor.Id);
                if(item.Bytes is null)return NotFound();
                Response.Headers["Cache-Control"]="private, no-store";
                Response.Headers["X-Content-Type-Options"]="nosniff";
                return File(item.Bytes,item.Mime);
            }
            catch(CryptographicException)
            {return StatusCode(503,new OperationResult("La clave de mensajes del servidor es incorrecta."));}
        }
        [HttpPost("{id:guid}/participants")]
        public async Task<IActionResult> Add(Guid id,[FromBody] Conversation.AddParticipantData data)
        {
            var actor=await CommunityAsync();
            return actor is null?Unauthorized():await conversations.AddParticipantAsync(id,actor.Id,data.UserId)?
                Ok(new OperationResult("Participante agregado.")):BadRequest(new OperationResult("No puedes agregar a esa persona."));
        }
        [HttpPost("{id:guid}/leave")]
        public async Task<IActionResult> Leave(Guid id)
        {
            var actor=await CommunityAsync();
            return actor is null?Unauthorized():await conversations.LeaveAsync(id,actor.Id)?
                Ok(new OperationResult("Saliste de la conversación.")):BadRequest(new OperationResult("No puedes abandonar esta sala."));
        }
        private IActionResult EncryptionUnavailable()=>StatusCode(503,new OperationResult("Mensajería no configurada. Define MU_MESSAGE_KEY_BASE64 en los secretos de Server y reinícialo; conserva siempre la clave original."));
        private async Task<ApiUser?> CommunityAsync()
        {
            var user=await accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
            return user?.Kind=="Community"?user:null;
        }
        #endregion Task
    }
}
