using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Models.Notifications;
using MujeresUnidas.Server.Models.User.Services;

namespace MujeresUnidas.Server.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    public sealed class NotificationsController(NotificationRepository notifications,AccountService accounts) : ControllerBase
    {
        #region Task
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var actor=await accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
            return actor is null?Unauthorized():Ok(await notifications.GetAsync(actor.Id));
        }
        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> Read(Guid id)
        {
            var actor=await accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
            return actor is null?Unauthorized():await notifications.ReadAsync(id,actor.Id)?Ok():NotFound();
        }
        #endregion Task
    }
}
