using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.User.Enums;
using BaseUser = global::MujeresUnidas.Server.Models.User.Base.User;
using MujeresUnidas.Server.Models.User.Subclasses;
namespace MujeresUnidas.Server.Controllers
{
[ApiController]
[Route("api")]
public sealed class AccountsController : ControllerBase
{
    #region Operation Data
    private readonly AccountService _accounts;
    private readonly CommunityMember _community;
    private readonly ManagementUser _management;
    private readonly OwnerUser _owner;
    #endregion Operation Data

    #region Initialization
    public AccountsController(AccountService accounts)
    {
        _accounts = accounts;
        _community = new CommunityMember(accounts);
        _management = new ManagementUser(accounts);
        _owner = new OwnerUser(accounts);
    }
    #endregion Initialization

    #region Task
    [HttpGet("setup/status")]
    public async Task<IActionResult> SetupStatus()
    {
        if (!await _accounts.SchemaCurrentAsync())
            return StatusCode(503,new OperationResult("La base demo no tiene el esquema R2. Ejecuta los scripts de instruccion.md en una base de PRUEBAS."));
        return Ok(new { ownerExists = await _accounts.OwnerExistsAsync() });
    }

    [HttpPost("setup/owner")]
    public async Task<IActionResult> SetupOwner([FromBody] OwnerUser.SetupData request)
    {
        string outcome = await _owner.RegisterAsync(request);
        return outcome switch
        {
            "OK" => Ok(new OperationResult("Autoridad principal configurada.")),
            "ALREADY_CONFIGURED" => Conflict(new OperationResult("El sistema ya tiene una autoridad principal.")),
            "SETUP_KEY_MISSING" => StatusCode(503, new OperationResult("Configure MU_OWNER_SETUP_KEY en el servidor.")),
            "SCHEMA_OUTDATED" => StatusCode(503, new OperationResult("La base local es de una versión anterior. Sigue el restablecimiento de prueba descrito en instruccion.md.")),
            "INVALID_SETUP_KEY" => Unauthorized(new OperationResult("Clave de configuración incorrecta.")),
            _ => BadRequest(new OperationResult("Datos inválidos."))
        };
    }

    [HttpPost("auth/register")]
    public async Task<IActionResult> Register([FromBody] CommunityMember.RegistrationData request)
    {
        string result = await _community.RegisterAsync(request);
        return result switch
        {
            "OK" => Ok(new OperationResult("Registro recibido. La cuenta permanece pendiente de activación presencial simulada.")),
            "DUPLICATE" => Conflict(new OperationResult("Correo o CURP ya registrados.")),
            "SCHEMA_OUTDATED" => StatusCode(503, new OperationResult("Esquema SQL anterior: StaffRole no admite NULL. Restablece únicamente la base de pruebas según instruccion.md.")),
            _ => BadRequest(new OperationResult("Datos inválidos."))
        };
    }

    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] BaseUser.LoginData request, [FromQuery] string? application = null)
    {
        if (application is not ("Community" or "Management" or "Owner"))
            return BadRequest(new OperationResult("Indica la aplicación Community, Management u Owner."));
        var (result, status) = application switch
        {
            "Community" => await _community.LoginAsync(request),
            "Owner" => await _owner.LoginAsync(request),
            _ => await _management.LoginAsync(request)
        };
        return status switch
        {
            "OK" => Ok(result),
            "PENDING" => StatusCode(403, new OperationResult("Cuenta pendiente de activación.")),
            "BLOCKED" => StatusCode(403, new OperationResult("Cuenta bloqueada.")),
            "WRONG_APPLICATION" => StatusCode(403, new OperationResult("Esta cuenta pertenece a la otra aplicación.")),
            _ => Unauthorized(new OperationResult("Credenciales no válidas."))
        };
    }

    [HttpPost("auth/logout")]
    public async Task<IActionResult> Logout()
    {
        await _accounts.LogoutAsync(Request.Headers["Authorization"].ToString());
        return Ok(new OperationResult("Sesión finalizada."));
    }

    [HttpGet("auth/me")]
    public async Task<IActionResult> Me()
    {
        var user = await CurrentUserAsync();
        return user is null ? Unauthorized() : Ok(user);
    }

    [HttpGet("accounts/pending")]
    public async Task<IActionResult> Pending()
    {
        var actor = await CurrentUserAsync();
        if (!CanManageCommunity(actor)) return StatusCode(403, new OperationResult("Acceso no autorizado."));
        return Ok((await _community.GetAccountsAsync()).Where(a => a.Status == "Pending"));
    }

    [HttpGet("accounts")]
    public async Task<IActionResult> AllAccounts()
    {
        var actor = await CurrentUserAsync();
        if (!CanManageCommunity(actor)) return StatusCode(403, new OperationResult("Acceso no autorizado."));
        return Ok(await _community.GetAccountsAsync());
    }

    [HttpPost("accounts/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var actor = await CurrentUserAsync();
        if (!CanManageCommunity(actor)) return StatusCode(403, new OperationResult("Acceso no autorizado."));
        bool changed = actor!.Kind == "Owner"
            ? await _owner.AcceptAccountAsync(id, actor.Id)
            : await _community.ChangeStatusAsync(id, AccountStatus.Active, actor.Id);
        return changed
            ? Ok(new OperationResult("Cuenta aprobada desde Gestión (revisión simulada)."))
            : NotFound(new OperationResult("La cuenta no existe o ya está activa."));
    }

    [HttpPost("accounts/{id:guid}/block")]
    public async Task<IActionResult> Block(Guid id)
    {
        var actor = await CurrentUserAsync();
        if (!CanManageCommunity(actor)) return StatusCode(403, new OperationResult("Acceso no autorizado."));
        return await _community.ChangeStatusAsync(id,AccountStatus.Blocked,actor!.Id)
            ? Ok(new OperationResult("Cuenta bloqueada.")) : NotFound();
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] CommunityMember.ProfileData request)
    {
        var user = await CurrentUserAsync();
        if (user?.Kind != "Community") return StatusCode(403, new OperationResult("Acceso no autorizado."));
        var updated = await _community.UpdateProfileAsync(user.Id,request);
        return updated is null ? BadRequest() : Ok(updated);
    }

    [HttpGet("management/summary")]
    public async Task<IActionResult> Summary()
    {
        var actor=await CurrentUserAsync();
        if (!CanManageCommunity(actor)) return StatusCode(403,new OperationResult("Acceso no autorizado."));
        return Ok(await _management.GetSummaryAsync());
    }

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff([FromBody] ManagementUser.StaffRegistrationData request)
    {
        var actor=await CurrentUserAsync();
        if (actor?.Kind != "Owner") return StatusCode(403,new OperationResult("Solo la autoridad principal puede registrar personal."));
        string result=await _owner.CreateStaffAsync(request,actor.Id);
        return result switch
        {
            "OK" => Ok(new OperationResult("Cuenta institucional creada. Entrega las credenciales por un canal seguro.")),
            "DUPLICATE" => Conflict(new OperationResult("El correo ya se encuentra registrado.")),
            "SCHEMA_OUTDATED" => StatusCode(503, new OperationResult("La base local está desactualizada. Sigue instruccion.md para reiniciarla.")),
            _ => BadRequest(new OperationResult("Revisa los datos y el rol institucional."))
        };
    }


    [HttpGet("management/roles")]
    public async Task<IActionResult> Roles()
    {
        var actor=await CurrentUserAsync();
        if(actor?.Kind!="Owner") return StatusCode(403,new OperationResult("Solo la autoridad principal puede administrar roles."));
        return Ok(await _owner.GetRolesAsync());
    }

    [HttpPost("management/roles")]
    public async Task<IActionResult> AddRole([FromBody] OwnerUser.RoleRegistrationData request)
    {
        var actor=await CurrentUserAsync();
        if(actor?.Kind!="Owner") return StatusCode(403,new OperationResult("Solo la autoridad principal puede administrar roles."));
        string result=await _owner.AddRoleAsync(request);
        return result switch
        {
            "OK"=>Ok(new OperationResult("Puesto institucional agregado.")),
            "DUPLICATE"=>Conflict(new OperationResult("El puesto ya está registrado.")),
            _=>BadRequest(new OperationResult("El nombre debe contener entre 3 y 100 caracteres."))
        };
    }
    private static bool CanManageCommunity(ApiUser? actor) => actor is not null &&
        (actor.Kind == "Owner" || (actor.Kind == "Staff" && actor.Role == "Administrator"));

    private Task<ApiUser?> CurrentUserAsync() => _accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
    #endregion Task
}
}
