using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Profiles;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Community-only public directory and authenticated media endpoints.</summary>
    [ApiController]
    [Route("api/community/profiles")]
    public sealed class ProfilesController(ProfileRepository profiles,PrivateMediaStore media,AccountService accounts) : ControllerBase
    {
        #region Task
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? q="")
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            string term=(q??string.Empty).Trim();
            if(term.Length>80)return BadRequest(new OperationResult("Búsqueda demasiado larga."));
            return Ok(await profiles.SearchAsync(actor.Id,term));
        }
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            var result=await profiles.GetAsync(actor.Id,id);
            return result is null?NotFound():Ok(result);
        }
        [HttpPost("{id:guid}/follow")]
        public async Task<IActionResult> Follow(Guid id)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            return await profiles.FollowAsync(actor.Id,id)?Ok(new OperationResult("Ahora sigues este perfil.")):
                BadRequest(new OperationResult("Perfil no disponible o ya lo sigues."));
        }
        [HttpDelete("{id:guid}/follow")]
        public async Task<IActionResult> Unfollow(Guid id)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            return await profiles.UnfollowAsync(actor.Id,id)?Ok(new OperationResult("Dejaste de seguir este perfil.")):NotFound();
        }
        [HttpGet("{id:guid}/followers")]
        public async Task<IActionResult> Followers(Guid id)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            return await profiles.GetAsync(actor.Id,id) is null?NotFound():Ok(await profiles.ConnectionsAsync(actor.Id,id,true));
        }
        [HttpGet("{id:guid}/following")]
        public async Task<IActionResult> Following(Guid id)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            return await profiles.GetAsync(actor.Id,id) is null?NotFound():Ok(await profiles.ConnectionsAsync(actor.Id,id,false));
        }
        [HttpPost("me/{kind:regex(avatar|cover)}")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> Upload(string kind,IFormFile file)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            var result=await media.SaveAsync(file,kind=="avatar");
            if(result is null)return BadRequest(new OperationResult("Usa JPEG o PNG. Foto máximo 2 MB; portada máximo 4 MB."));
            try
            {
                string? old=await profiles.SetMediaAsync(actor.Id,kind=="avatar",result.Value.Key);
                media.Remove(old);
                return Ok(new OperationResult("Imagen guardada."));
            }
            catch
            {
                media.Remove(result.Value.Key);
                throw;
            }
        }
        [HttpGet("{id:guid}/{kind:regex(avatar|cover)}")]
        public async Task<IActionResult> Image(Guid id,string kind)
        {
            var actor=await CommunityAsync();
            if(actor is null)return Unauthorized();
            if(await profiles.GetAsync(actor.Id,id) is null)return NotFound();
            string? key=await profiles.MediaFileAsync(id,kind=="avatar");
            var file=media.Find(key);
            if(file is null)return NotFound();
            Response.Headers["X-Content-Type-Options"]="nosniff";
            Response.Headers["Cache-Control"]="private, max-age=300";
            return PhysicalFile(file.Value.Path,file.Value.ContentType);
        }
        private async Task<ApiUser?> CommunityAsync()
        {
            var actor=await accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
            return actor?.Kind=="Community"?actor:null;
        }
        #endregion Task
    }
}
