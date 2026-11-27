using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Publications;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.Publications.Repositories;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Community content API. Staff rights come from the authenticated SQL account, never request fields.</summary>
    [ApiController]
    [Route("api/posts")]
    public sealed class PostsController : ControllerBase
    {
        #region Operation Data
        private readonly Post _posts;
        private readonly AccountService _accounts;
        private readonly PostRepository _repository;
        private readonly PostMediaStore _media;
        #endregion Operation Data
        #region Initialization
        public PostsController(Post posts, AccountService accounts,PostRepository repository,PostMediaStore media)
        {
            _posts = posts;
            _accounts = accounts;
            _repository=repository;_media=media;
        }
        #endregion Initialization
        #region Task
        [HttpGet]
        public async Task<IActionResult> Feed([FromQuery] string? category = null, [FromQuery] bool featured = false)
        {
            if (await CurrentAsync() is not { Kind: "Community" }) return Unauthorized();
            if (category is not null && category is not ("Moment" or "Blog" or "Guide" or "Job" or "Event"))
                return BadRequest(new OperationResult("Categoría desconocida."));
            return Ok(await _posts.GetFeedAsync(category, featured));
        }

        [HttpGet("by-author/{id:guid}")]
        public async Task<IActionResult> ByAuthor(Guid id)
        {
            if (await CurrentAsync() is not { Kind:"Community" }) return Unauthorized();
            return Ok(await _posts.GetByAuthorAsync(id));
        }

        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var actor = await CurrentAsync();
            return actor?.Kind != "Community" ? Unauthorized() : Ok(await _posts.GetMineAsync(actor.Id));
        }

        /// <summary>Community member's saved approved posts, separate from private account data.</summary>
        [HttpGet("saved")]
        public async Task<IActionResult> Saved()
        {
            var actor=await CurrentAsync();
            return actor?.Kind!="Community"?Unauthorized():Ok(await _repository.GetSavedAsync(actor.Id));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Post.CreateData request)
        {
            var actor = await CurrentAsync();
            if (actor?.Kind != "Community") return Unauthorized();
            if (request.Category is not ("Moment" or "Blog" or "Guide" or "Job" or "Event"))
                return BadRequest(new OperationResult("Tipo de publicación desconocido."));
            var validation = _posts.ForCategory(request.Category).ValidateCreate(request);
            if (!validation.Success) return BadRequest(new OperationResult(validation.Message));
            bool staff = IsStaff(actor);
            if(!staff && request.Category is not ("Blog" or "Moment"))
                return StatusCode(403,new OperationResult("Solo Staff puede solicitar guías, empleos y eventos."));
            bool autoApprove=staff && request.Category is ("Blog" or "Moment");
            Guid id = await _posts.CreatePostAsync(actor.Id,request,autoApprove);
            return Ok(new { id, status = autoApprove ? "Aprobada" : "Pendiente", message = autoApprove
                ? "Publicación realizada." : "Publicación enviada a revisión." });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Post.EditData request)
        {
            var actor = await CurrentAsync();
            if (actor?.Kind != "Community") return Unauthorized();
            var validation=await _posts.ValidateEditAsync(id,request);
            if(!validation.Success) return BadRequest(new OperationResult(validation.Message));
            return await _posts.EditPostAsync(id, actor.Id, IsStaff(actor), request)
                ? Ok(new OperationResult("Cambios guardados. Si eres miembro, vuelven a revisión.")) : NotFound();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var actor = await CurrentAsync();
            if(actor is null)return Unauthorized();
            if(IsInstitutionalReviewer(actor))
                return await _repository.DeleteReviewedAsync(id)?Ok(new OperationResult("Retiro institucional registrado.")):NotFound();
            if(IsStaff(actor))
                return await _posts.DeletePostAsync(id,actor.Id,true)?Ok(new OperationResult("Blog retirado por moderación.")):NotFound();
            if(actor.Kind=="Community")
                return await _repository.RequestDeletionAsync(id,actor.Id)?Ok(new OperationResult("Solicitud de retiro enviada a Gestión.")):
                    Conflict(new OperationResult("La publicación no existe, no es tuya o ya tiene retiro solicitado."));
            return StatusCode(403);
        }
        [HttpPost("{id:guid}/deletion-request")]
        public async Task<IActionResult> RequestDeletion(Guid id)
        {
            var actor=await CurrentAsync();if(actor?.Kind!="Community")return Unauthorized();
            return await _repository.RequestDeletionAsync(id,actor.Id)
                ?Ok(new OperationResult("Tu solicitud será revisada por Gestión.")):
                Conflict(new OperationResult("No puedes solicitar el retiro de esta publicación."));
        }
        [HttpGet("institutional/deletions/pending")]
        public async Task<IActionResult> PendingDeletion()
        {
            if(!IsInstitutionalReviewer(await CurrentAsync()))return StatusCode(403);
            return Ok(await _repository.PendingDeletionAsync());
        }
        [HttpPost("institutional/deletions/{id:guid}/review")]
        public async Task<IActionResult> ReviewDeletion(Guid id,[FromBody] Post.ReviewData decision)
        {
            var actor=await CurrentAsync();if(!IsInstitutionalReviewer(actor))return StatusCode(403);
            return await _repository.ReviewDeletionAsync(id,actor!.Id,decision.Approved)
                ?Ok(new OperationResult("Solicitud de retiro revisada.")):NotFound();
        }

        /// <summary>Uploads an actual JPEG/PNG to a private server folder, not wwwroot.</summary>
        [HttpPost("{id:guid}/media")]
        [RequestSizeLimit(3_000_000)]
        public async Task<IActionResult> UploadMedia(Guid id,IFormFile file)
        {
            var actor=await CurrentAsync();
            if(actor?.Kind!="Community")return Unauthorized();
            var saved=await _media.SaveAsync(file);
            if(saved is null)return BadRequest(new OperationResult("PNG o JPEG, máximo 2 MB por imagen."));
            try
            {
                Guid? mediaId=await _repository.AddMediaAsync(id,actor.Id,saved.Value.Key,
                    saved.Value.ContentType,IsStaff(actor));
                if(!mediaId.HasValue)
                {
                    _media.Remove(saved.Value.Key);
                    return BadRequest(new OperationResult("Solo la autora puede subir hasta cinco imágenes a una publicación activa."));
                }
                return Ok(new {mediaId=mediaId.Value,message="Imagen agregada. Los cambios pueden requerir revisión."});
            }
            catch{_media.Remove(saved.Value.Key);throw;}
        }
        /// <summary>Uploads the post's separate preview cover (one cover plus five gallery photos).</summary>
        [HttpPost("{id:guid}/cover")]
        [RequestSizeLimit(3_000_000)]
        public async Task<IActionResult> UploadCover(Guid id,IFormFile file)
        {
            var actor=await CurrentAsync();if(actor?.Kind!="Community")return Unauthorized();
            var saved=await _media.SaveAsync(file);
            if(saved is null)return BadRequest(new OperationResult("Portada JPEG/PNG de máximo 2 MiB."));
            try
            {
                var result=await _repository.SaveCoverAsync(id,actor.Id,saved.Value.Key,saved.Value.ContentType,IsStaff(actor));
                if(!result.Success){_media.Remove(saved.Value.Key);return StatusCode(403,new OperationResult("Solo la autora puede cambiar la portada."));}
                if(result.OldKey is not null)_media.Remove(result.OldKey);
                return Ok(new OperationResult("Portada guardada."));
            }
            catch{_media.Remove(saved.Value.Key);throw;}
        }
        [HttpGet("{id:guid}/cover")]
        public async Task<IActionResult> Cover(Guid id)
        {
            var actor=await CurrentAsync();if(actor is null)return Unauthorized();
            if(!await _repository.CanReadMediaAsync(id,actor.Id,IsStaff(actor),IsInstitutionalReviewer(actor)))return NotFound();
            var mediaId=await _repository.CoverIdAsync(id);
            if(!mediaId.HasValue)return NotFound();
            var key=await _repository.MediaKeyAsync(id,mediaId.Value);
            var file=key is null?null:_media.Find(key);
            if(file is null)return NotFound();
            Response.Headers["X-Content-Type-Options"]="nosniff";
            Response.Headers["Cache-Control"]="private, max-age=180";
            return PhysicalFile(file.Value.Path,file.Value.ContentType);
        }
        [HttpGet("{id:guid}/media")]
        public async Task<IActionResult> Media(Guid id)
        {
            var actor=await CurrentAsync();
            if(actor is null)return Unauthorized();
            if(!await _repository.CanReadMediaAsync(id,actor.Id,IsStaff(actor),IsInstitutionalReviewer(actor)))return NotFound();
            return Ok(await _repository.MediaAsync(id));
        }
        [HttpGet("{id:guid}/media/{mediaId:guid}")]
        public async Task<IActionResult> Image(Guid id,Guid mediaId)
        {
            var actor=await CurrentAsync();
            if(actor is null)return Unauthorized();
            if(!await _repository.CanReadMediaAsync(id,actor.Id,IsStaff(actor),IsInstitutionalReviewer(actor)))return NotFound();
            string? key=await _repository.MediaKeyAsync(id,mediaId);
            var file=key is null?null:_media.Find(key);
            if(file is null)return NotFound();
            Response.Headers["X-Content-Type-Options"]="nosniff";
            Response.Headers["Cache-Control"]="private, max-age=180";
            return PhysicalFile(file.Value.Path,file.Value.ContentType);
        }
        [HttpGet("pending")]
        public async Task<IActionResult> Pending()
        {
            if (!IsStaff(await CurrentAsync())) return StatusCode(403,new OperationResult("Solo Staff puede moderar."));
            return Ok(await _posts.GetPendingAsync());
        }

        [HttpPost("{id:guid}/review")]
        public async Task<IActionResult> Review(Guid id, [FromBody] Post.ReviewData request)
        {
            var actor = await CurrentAsync();
            if (!IsStaff(actor)) return StatusCode(403,new OperationResult("Solo Staff puede moderar."));
            return await _posts.ReviewPostAsync(id,actor!.Id,request)
                ? Ok(new OperationResult(request.Approved ? "Publicación aprobada." : "Publicación rechazada."))
                : NotFound(new OperationResult("Solicitud no encontrada o ya revisada."));
        }

        [HttpGet("institutional/pending")]
        public async Task<IActionResult> InstitutionalPending()
        {
            var actor=await CurrentAsync();
            if(!IsInstitutionalReviewer(actor))return StatusCode(403,new OperationResult("Revisión institucional no autorizada."));
            return Ok(await _posts.GetInstitutionalPendingAsync());
        }
        [HttpPost("institutional/{id:guid}/review")]
        public async Task<IActionResult> InstitutionalReview(Guid id,[FromBody] Post.ReviewData request)
        {
            var actor=await CurrentAsync();
            if(!IsInstitutionalReviewer(actor))return StatusCode(403,new OperationResult("Revisión institucional no autorizada."));
            return await _posts.ReviewInstitutionalAsync(id,actor!.Id,request)
                ?Ok(new OperationResult(request.Approved?"Publicación institucional aprobada.":"Publicación rechazada."))
                :NotFound(new OperationResult("No existe solicitud institucional pendiente."));
        }
        private static bool IsInstitutionalReviewer(ApiUser? actor)=>actor is not null &&
            (actor.Kind=="Owner" || (actor.Kind=="Staff" && actor.Role=="Administrator"));

        private bool IsStaff(ApiUser? actor) => actor is { Kind:"Community" } &&
            MujeresUnidas.Server.Models.User.Subclasses.CommunityMember.ForRole(_accounts,actor.CommunityRole).CanModerateBlogs;
        private Task<ApiUser?> CurrentAsync() => _accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
        #endregion Task
    }
}
