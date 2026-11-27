using Microsoft.AspNetCore.Mvc;
using MujeresUnidas.Server.Contracts;
using MujeresUnidas.Server.Models.User.Services;
using MujeresUnidas.Server.Models.User.Enums;
using MujeresUnidas.Server.Models.User.Profiles;
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
    private readonly ProfileRepository _profiles;
    private readonly PrivateMediaStore _media;
    #endregion Operation Data
    #region Initialization
    public AccountsController(AccountService accounts,ProfileRepository profiles,PrivateMediaStore media)
    {
        _accounts = accounts;
        _profiles=profiles;_media=media;
        _community = new CommunityMember(accounts);
        _management = new ManagementUser(accounts);
        _owner = new OwnerUser(accounts);
    }
    #endregion Initialization
    #region Task
    [HttpGet("setup/status")]
    public async Task<IActionResult> SetupStatus()
    {
        try
        {
            if (!await _accounts.SchemaCurrentAsync())
                return StatusCode(503,new OperationResult("El esquema de Mujeres Unidas está incompleto. Verifica las migraciones 150, 155, 170 y 190 sin borrar datos."));
            return Ok(new { ownerExists = await _accounts.OwnerExistsAsync() });
        }
        catch(Microsoft.Data.SqlClient.SqlException)
        { return StatusCode(503,new OperationResult("Base de datos no disponible. Contacta a la administración.")); }
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
            "SCHEMA_OUTDATED" => StatusCode(503, new OperationResult("Base de datos desactualizada. Ejecuta las migraciones incrementales sin borrar datos.")),
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
            "OK" => Ok(new {message="Solicitud recibida. Cuenta en espera de verificación y aprobación. En esta simulación puedes consultar las sedes ficticias de Ahome; no existe fecha límite automática ni se trata de una cita real.",
                proposedVisitUtc=(DateTime?)null,locationLabel="Sedes de Ahome (demostración)"}),
            "DUPLICATE" => Conflict(new OperationResult("Correo o CURP ya registrados.")),
            "ELIGIBILITY_REQUIRED" => BadRequest(new OperationResult("Esta comunidad está dirigida a mujeres. Confirma voluntariamente las condiciones para solicitar acceso.")),
            "SCHEMA_OUTDATED" => StatusCode(503, new OperationResult("Esquema SQL anterior; aplica las migraciones incrementales y verifica StaffRole, sin borrar cuentas.")),
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
            "WRONG_APPLICATION" => StatusCode(403, new OperationResult("Tu cuenta no tiene acceso autorizado a esta aplicación.")),
            "SCHEMA_OUTDATED" => StatusCode(503, new OperationResult("Servidor en mantenimiento: falta actualizar el esquema SQL de sesiones. Contacta a la administración.")),
            "DATABASE_UNAVAILABLE" => StatusCode(503, new OperationResult("Servicio temporalmente no disponible. Inténtalo más tarde.")),
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

    [HttpPost("management/accounts/{id:guid}/verify-demo")]
    public async Task<IActionResult> VerifyDemo(Guid id)
    {
        var actor=await CurrentUserAsync();
        if(!CanManageCommunity(actor))return StatusCode(403);
        return await _accounts.VerifyDemoIdentityAsync(id,actor!.Id)?
            Ok(new OperationResult("Se registró verificación de identidad SIMULADA, sin documentos reales.")):
            BadRequest(new OperationResult("La cuenta no está pendiente o no existe."));
    }
    /// <summary>Records a simulated in-person verification at a chosen authorized demo administrative site.</summary>
    public sealed record VisitSiteData(Guid SiteId);
    [HttpPost("management/accounts/{id:guid}/verify-at-site")]
    public async Task<IActionResult> VerifyAtSite(Guid id,[FromBody] VisitSiteData input)
    {
        var actor=await CurrentUserAsync();
        if(!CanManageCommunity(actor))return StatusCode(403);
        if(input.SiteId==Guid.Empty)return BadRequest(new OperationResult("Selecciona una sede de Ahome."));
        return await _accounts.VerifyDemoIdentityAsync(id,actor!.Id,input.SiteId)?
            Ok(new OperationResult("Verificación simulada registrada en la sede elegida.")):
            BadRequest(new OperationResult("La cuenta no está pendiente o la sede no admite verificaciones."));
    }

    [HttpPost("accounts/{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id,[FromBody] CommunityMember.ActivationData decision)
    {
        var actor = await CurrentUserAsync();
        if (!CanManageCommunity(actor)) return StatusCode(403, new OperationResult("Acceso no autorizado."));
        if (decision.CommunityRole is not ("Member" or "Staff"))
            return BadRequest(new OperationResult("Gestión debe seleccionar Miembro o Staff."));
        bool changed = actor!.Kind == "Owner"
            ? await _owner.AcceptAccountAsync(id, actor.Id,decision.CommunityRole)
            : await _community.ChangeStatusAsync(id, AccountStatus.Active, actor.Id,decision.CommunityRole);
        return changed
            ? Ok(new OperationResult("Cuenta aprobada con el rol asignado por Gestión (revisión simulada)."))
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

    [HttpPut("management/profile")]
    public async Task<IActionResult> UpdateInstitutionalProfile([FromBody] ManagementUser.ProfileData request)
    {
        var actor=await CurrentUserAsync();
        if(actor?.Kind is not ("Owner" or "Staff"))return StatusCode(403,new OperationResult("Sin acceso institucional."));
        var result=await _accounts.UpdateInstitutionalProfileAsync(actor.Id,request);
        return result is null?BadRequest(new OperationResult("Datos de perfil inválidos.")):Ok(result);
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
            "OK" => Ok(new OperationResult("Invitación institucional pendiente. Tras verificar identidad, activa en Personal. Debe cambiar su contraseña inicial.")),
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
    [HttpPost("accounts/me/{kind:regex(avatar|cover)}")]
    [RequestSizeLimit(5*1024*1024)]
    public async Task<IActionResult> UpdateMyImage(string kind,IFormFile file)
    {
        var actor=await CurrentUserAsync();if(actor is null)return Unauthorized();
        var stored=await _media.SaveAsync(file,kind=="avatar");
        if(stored is null)return BadRequest(new OperationResult("Archivo JPEG/PNG demasiado grande o inválido."));
        try
        {
            var old=await _profiles.SetMediaAsync(actor.Id,kind=="avatar",stored.Value.Key,actor.Kind=="Community"?"Community":"Management");
            _media.Remove(old);return Ok(new OperationResult("Fotografía guardada."));
        }
        catch{_media.Remove(stored.Value.Key);throw;}
    }
    [HttpGet("accounts/me/{kind:regex(avatar|cover)}")]
    public async Task<IActionResult> MyImage(string kind)
    {
        var actor=await CurrentUserAsync();if(actor is null)return Unauthorized();
        var key=await _profiles.MediaFileAsync(actor.Id,kind=="avatar",actor.Kind=="Community"?"Community":"Management");
        var result=_media.Find(key);if(result is null)return NotFound();
        Response.Headers["Cache-Control"]="private, no-store";
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return PhysicalFile(result.Value.Path,result.Value.ContentType);
    }
    [HttpGet("accounts/me/details")]
    public async Task<IActionResult> MyDetails()
    {
        var actor=await CurrentUserAsync();return actor is null?Unauthorized():Ok(await _accounts.MyDetailsAsync(actor.Id));
    }
    [HttpPut("accounts/me/details")]
    public async Task<IActionResult> SaveDetails([FromBody] BaseUser.PersonalDetailsData data)
    {
        var actor=await CurrentUserAsync();if(actor is null)return Unauthorized();
        return await _accounts.UpdateMyDetailsAsync(actor.Id,data)?Ok(new OperationResult("Datos privados guardados.")):
            BadRequest(new OperationResult("Verifica campos, CURP/RFC de prueba y fecha."));
    }
    [HttpPut("auth/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] BaseUser.ChangePasswordData data)
    {
        var actor=await CurrentUserAsync();if(actor is null)return Unauthorized();
        return await _accounts.ChangePasswordAsync(actor.Id,data)?Ok(new OperationResult("Contraseña cambiada. Todas las sesiones fueron cerradas; inicia sesión nuevamente.")):
            BadRequest(new OperationResult("Contraseña actual incorrecta o nueva contraseña inválida."));
    }
    [HttpGet("management/staff")]
    public async Task<IActionResult> StaffDirectory()
    {
        var actor=await CurrentUserAsync();if(actor?.Kind!="Owner")return StatusCode(403);
        var items=await _accounts.StaffAccountsAsync();
        return Ok(items.Select(s=>new{id=s.Id,displayName=s.Name,email=s.Email,role=s.Role,status=s.Status}));
    }
    [HttpPost("management/staff/{id:guid}/verify-demo")]
    public async Task<IActionResult> VerifyStaffDemo(Guid id)
    {
        var actor=await CurrentUserAsync();if(actor?.Kind!="Owner")return StatusCode(403);
        return await _accounts.VerifyDemoIdentityAsync(id,actor.Id)?Ok(new OperationResult("Verificación institucional simulada registrada.")):
            BadRequest(new OperationResult("Cuenta no pendiente."));
    }
    [HttpPost("management/staff/{id:guid}/activate")]
    public async Task<IActionResult> ActivateStaff(Guid id)
    {
        var actor=await CurrentUserAsync();if(actor?.Kind!="Owner")return StatusCode(403);
        return await _accounts.ActivateStaffAsync(id,actor.Id)?Ok(new OperationResult("Personal verificado y activado (demostración).")):
            BadRequest(new OperationResult("Cuenta inexistente o ya revisada."));
    }
    private static bool CanManageCommunity(ApiUser? actor) => actor is not null &&
        (actor.Kind == "Owner" || (actor.Kind == "Staff" && actor.Role == "Administrator"));

    private Task<ApiUser?> CurrentUserAsync() => _accounts.GetUserByTokenAsync(Request.Headers["Authorization"].ToString());
    #endregion Task
}
}
