using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Publications.Engagement;
using MujeresUnidas.Server.Models.Publications.Repositories;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers
{
    /// <summary>Community post interactions, independent from care case data and institutional chat.</summary>
    [ApiController]
    [Route("api/posts")]
    public sealed class PostEngagementController(PostEngagementRepository repository,AccountService accounts) : ControllerBase
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
        private async Task<ApiUser?> CommunityAsync()
        {
            var actor=await accounts.GetUserByTokenAsync(Request.Headers.Authorization.ToString());
            return actor?.Kind=="Community"?actor:null;
        }
        [HttpGet("{id:guid}/engagement")]
        public async Task<IActionResult> Read(Guid id)
        {
            var actor=await CommunityAsync();if(actor is null)return StatusCode(403);
            var result=await repository.ReadAsync(id,actor.Id);
            return result.HasValue?Ok(result.Value):NotFound(new OperationResult("La publicación no está aprobada o no existe."));
        }
        [HttpPut("{id:guid}/like")]
        public async Task<IActionResult> Like(Guid id)=>await ReactAsync(id,true);
        [HttpDelete("{id:guid}/like")]
        public async Task<IActionResult> Unlike(Guid id)=>await ReactAsync(id,false);
        private async Task<IActionResult> ReactAsync(Guid id,bool add)
        {
            var actor=await CommunityAsync();if(actor is null)return StatusCode(403);
            return await repository.SetReactionAsync(id,actor.Id,add)?Ok(new OperationResult("Reacción actualizada.")):
                NotFound(new OperationResult("Publicación no disponible."));
        }
        [HttpPut("{id:guid}/save")]
        public async Task<IActionResult> Save(Guid id)=>await BookmarkAsync(id,true);
        [HttpDelete("{id:guid}/save")]
        public async Task<IActionResult> Unsave(Guid id)=>await BookmarkAsync(id,false);
        private async Task<IActionResult> BookmarkAsync(Guid id,bool add)
        {
            var actor=await CommunityAsync();if(actor is null)return StatusCode(403);
            return await repository.SetBookmarkAsync(id,actor.Id,add)?Ok(new OperationResult("Guardado actualizado.")):
                NotFound(new OperationResult("Publicación no disponible."));
        }
        [HttpPost("{id:guid}/comments")]
        public async Task<IActionResult> Comment(Guid id,[FromBody] PostEngagement.CommentCreateData data)
        {
            var actor=await CommunityAsync();if(actor is null)return StatusCode(403);
            var body=data.Body?.Trim();
            if(string.IsNullOrWhiteSpace(body)||body.Length>600)
                return BadRequest(new OperationResult("Escribe un comentario de 1 a 600 caracteres."));
            var result=await repository.AddCommentAsync(id,actor.Id,body);
            return result.HasValue?Ok(new {commentId=result.Value}):NotFound(new OperationResult("Publicación no disponible."));
        }
        [HttpDelete("{id:guid}/comments/{commentId:guid}")]
        public async Task<IActionResult> DeleteComment(Guid id,Guid commentId)
        {
            var actor=await CommunityAsync();if(actor is null)return StatusCode(403);
            var staff=actor.CommunityRole=="Staff";
            return await repository.RemoveCommentAsync(id,commentId,actor.Id,staff)?
                Ok(new OperationResult("Comentario retirado.")):StatusCode(403,new OperationResult("No autorizado o comentario inexistente."));
        }
        #endregion Task
    }
}
