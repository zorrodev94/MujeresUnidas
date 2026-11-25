# Mujeres Unidas · EarlyAccess · R1 candidata

Prototipo académico del Hackathon FIM-UAS 2026, orientado a la comunidad de mujeres de Ahome, Sinaloa. **No representa un servicio institucional autorizado ni un canal de emergencias**. Esta copia es una **candidata para revisión**, no un release aprobado ni un commit realizado.

## Abrir en Visual Studio

Abre **`MujeresUnidas.slnx`** desde la raíz. `README.md`, `instruccion.md`, `LICENSE.md` y las reglas Git aparecen en **Solution Items**, junto a los tres proyectos:

| Proyecto | Tecnología | Responsabilidad |
| --- | --- | --- |
| `MujeresUnidas.Server` | ASP.NET Core Web API · .NET 10 · SQL Server | Registro, sesiones, activaciones, privilegios, auditoría y persistencia |
| `MujeresUnidas.Comunidad` | .NET MAUI · Windows / Android | Registro y acceso de Comunidad, Home ilustrado y perfil |
| `MujeresUnidas.Gestion` | Windows Forms · .NET 10 | Configuración Owner, acceso institucional, revisión y activación, altas de personal |

### Convenciones de ingeniería

- Namespace de **bloque** con `{ }`; una clase por archivo cuando corresponda.
- `Models/User/Base/User.cs` en cada cliente y en Server; contiene datos comunes, estructuras de operaciones y `#region Task`.
- Herencia de Server: `OwnerUser : User`, `ManagementUser : User`, `CommunityMember : User`. **Owner no hereda de ManagementUser**.
- Contrato de acceso `IAccountAccess<TRegistration>` combina `LoginAsync` y `RegisterAsync`, con tipos distintos para el registro de Owner y Comunidad.
- Las estructuras de registro y perfil son **internas a la clase correspondiente**; los métodos asíncronos ejecutan operaciones reales sobre la API o el servicio del servidor.
- `User.RoleUserGestion` y `User.RoleUserComunidad` conservan los catálogos iniciales en `#region Arrays`. La lista de puestos visible en Gestión se lee realmente de `dbo.ManagementRoles`.
- Los puestos son **configurables en SQL**, no un `InstitutionalRole` compilado. Crear un puesto nuevo habilita acceso básico de personal, **no** permisos de administrador automáticamente.
- Estados técnicos en `Models/User/Enums`: `AccountStatus` (Pending/Active/Blocked), `ActivationStatus` (AwaitingAppointment/Approved/Declined); `AccountKind` delimita los tres tipos técnicos de cuenta. No hay citas reales todavía.
- `Comunidad/Models` ejecuta solicitudes HTTP por medio de `Connection/ApiClient`; `Gestion/Models` usa `Connection/ApiConnection`. **Las ventanas no ejecutan consultas SQL ni envían HTTP directamente**.
- Se reservan modelos vacíos en `Server/Models/Messages` y `Server/Models/Publications` para iteraciones futuras. No simulan chat, blogs ni publicaciones funcionales.

## Instalación de prueba desde cero

Requisitos en Windows: Visual Studio con componentes ASP.NET Core, escritorio .NET, MAUI y SDK .NET 10; SQL Server 2022 y SSMS.

1. Extrae la carpeta `MujeresUnidas` completa y abre `MujeresUnidas.slnx`.
2. En **la instancia SQL de pruebas**, ejecuta `MujeresUnidas.Server/Infrastructure/Database/000_create_database.sql` **solamente después de eliminar cualquier esquema incompatible de revisiones anteriores**. Después ejecuta `020_verify_fresh_demo.sql` y confirma que `StaffRole` admite NULL. Crea tablas de cuentas, sesiones, auditoría y puestos.
3. Si ya existe una base de una revisión anterior, **solo en tu instancia local de pruebas**, detén Server, ejecuta `910_drop_demo_database.sql` habilitando deliberadamente `@Confirm=1`, y después vuelve a ejecutar `000_create_database.sql`. Elimina por completo las cuentas antiguas para cada demostración. **No apliques esos scripts en una base real**.
4. En la carpeta `MujeresUnidas.Server`, configura los secretos locales sin escribirlos en el repositorio:

   ```powershell
   dotnet user-secrets set "ConnectionStrings:Main" "Server=localhost;Database=MujeresUnidas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
   ```

   Utiliza tu instancia SQL real si difiere de `localhost`. `TrustServerCertificate=True` se usa **solo en este entorno de prueba**. El proyecto conserva el `UserSecretsId` local de R1; puedes verificarlo con `dotnet user-secrets list`.
5. Configura `MU_OWNER_SETUP_KEY` en los secretos del **proyecto Server** si no está configurada:

   ```powershell
   $bytes = New-Object byte[] 32
   $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
   $rng.GetBytes($bytes)
   $rng.Dispose()
   $setupKey = [Convert]::ToBase64String($bytes)
   dotnet user-secrets set "MU_OWNER_SETUP_KEY" $setupKey
   ```

   Guarda la clave fuera del repositorio y utilízala solamente para la primera cuenta `OwnerUser`.
6. Inicia **Server** con el perfil `https` y verifica `https://localhost:7013/health` y `https://localhost:7013/api/setup/status`. El primer endpoint no consulta SQL; el segundo sí.
7. Ejecuta **Gestión**. La primera vez aparece **Activar primera cuenta**, con el nombre, correo, contraseña y clave configurada; después aparece **Iniciar sesión**. La vista no debe ocultar campos por una fila de 0 % de alto.
8. Ejecuta **Comunidad** en `Windows Machine`. Se abre una ventana de tamaño parecido a un teléfono (430 × 700 aprox.), con un encabezado compacto de acceso; no hay que pasar por una imagen gigante para llegar al botón. Registra una cuenta con CURP completamente inventada de 18 caracteres, comprueba que queda pendiente y actívala desde Gestión. Verifica que recién entonces permite iniciar sesión. Si aparece **HTTP 503 / StaffRole**, el esquema SQL de pruebas debe restablecerse; no es un error de tus credenciales.
9. Ejecuta `Scripts/QA-Rebuild.ps1` y sigue los casos de `instruccion.md` **antes de cualquier commit**.

**Android:** desde el emulador, `10.0.2.2` representa la computadora anfitriona, pero el certificado de desarrollo HTTPS no se acepta automáticamente. La primera validación de R1 se realiza en Windows. No deshabilites la verificación TLS para corregir ese error.

## Qué está implementado en código

- Server: inicialización de un solo Owner, login de Comunidad y Gestión, registro comunitario pendiente, sesiones, consultas autorizadas, activación y bloqueo, alta de personal, catálogo de puestos extensible, auditoría y resumen institucional.
- Comunidad: login, registro, Home ilustrado y edición de nombre/biografía. Los demás módulos se identifican como demostración visual.
- Gestión: login, primera instalación, tablero, revisión de cuentas, activación/bloqueo, altas de personal y adición de puestos institucionales.
- Preparados **sin funcionalidad**: `Conversation`, `ChatMessage`, `Blog`, `Guide` y `EmploymentPost`.

**No implementado:** citas reales, verificación física de identidad, expedientes, chat, blogs dinámicos, envío de correos, notificaciones institucionales y alertas de emergencia. La operación de aprobación en el prototipo **no certifica una verificación presencial real**.

## Seguridad y limitaciones

Contraseñas derivadas mediante PBKDF2, sesiones almacenadas como digests SHA-256, SQL parametrizado y permisos comprobados en Server. El paquete `System.Security.Cryptography.Pkcs` se fijó a `10.0.13` por avisos de vulnerabilidad de una dependencia transitiva; confirmar con restauración NuGet en Windows. No se distribuyen claves de configuración.

**Esta revisión no fue compilada ni ejecutada dentro del entorno de preparación**: aquí no hay SDK .NET, Visual Studio ni SQL Server. Se requiere Rebuild Solution y pruebas funcionales en el equipo del proyecto. Consulta `instruccion.md` y el informe QA entregado fuera del ZIP.

## Derechos

Material para desarrollo y revisión académica interna de EarlyAccess. Ver `LICENSE.md`. Los derechos de cada ilustración se revisarán antes de cualquier distribución pública.
