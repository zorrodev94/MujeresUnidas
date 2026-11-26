using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.Publications;
using MujeresUnidas.Server.Models.User.Services;

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
        #endregion Operation Data

        #region Initialization
        public PostsController(Post posts, AccountService accounts)
        {
            _posts = posts;
            _accounts = accounts;
        }
        #endregion Initialization

        #region Task
        [HttpGet]
        public async Task<IActionResult> Feed([FromQuery] string? category = null, [FromQuery] bool featured = false)
        {
            if (await CurrentAsync() is not { Kind: "Community" }) return Unauthorized();
            if (category is not null && category is not ("Blog" or "Guide" or "Job" or "Event"))
                return BadRequest(new OperationResult("Categoría desconocida."));
            return Ok(await _posts.GetFeedAsync(category, featured));
        }

        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var actor = await CurrentAsync();
            return actor?.Kind != "Community" ? Unauthorized() : Ok(await _posts.GetMineAsync(actor.Id));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Post.CreateData request)
        {
            var actor = await CurrentAsync();
            if (actor?.Kind != "Community") return Unauthorized();
            if (!Valid(request)) return BadRequest(new OperationResult("Revisa título, resumen y contenido."));
            bool staff = IsStaff(actor);
            Guid id = await _posts.CreatePostAsync(actor.Id,request,staff);
            return Ok(new { id, status = staff ? "Aprobada" : "Pendiente", message = staff
                ? "Publicación creada." : "Solicitud enviada a moderación de Staff." });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Post.EditData request)
        {
            var actor = await CurrentAsync();
            if (actor?.Kind != "Community") return Unauthorized();
            if (!ValidText(request.Title, 4, 120) || !ValidText(request.Summary, 8, 260) ||
                !ValidText(request.Body, 20, 2500)) return BadRequest(new OperationResult("Contenido inválido."));
            return await _posts.EditPostAsync(id, actor.Id, IsStaff(actor), request)
                ? Ok(new OperationResult("Cambios guardados. Si eres miembro, vuelven a revisión.")) : NotFound();
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var actor = await CurrentAsync();
            if (actor?.Kind != "Community") return Unauthorized();
            return await _posts.DeletePostAsync(id, actor.Id, IsStaff(actor))
                ? Ok(new OperationResult("Publicación retirada.")) : NotFound();
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

        private static bool Valid(Post.CreateData data) =>
            (data.Category is "Blog" or "Guide" or "Job" or "Event") &&
            ValidText(data.Title,4,120) && ValidText(data.Summary,8,260) && ValidText(data.Body,20,2500);
        private static bool ValidText(string? text,int min,int max) =>
            !string.IsNullOrWhiteSpace(text) && text.Trim().Length>=min && text.Trim().Length<=max;
        private static bool IsStaff(ApiUser? actor) => actor is { Kind:"Community", CommunityRole:"Staff" };
        private Task<ApiUser?> CurrentAsync() => _accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
        #endregion Task
    }
}
