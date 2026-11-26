# Mujeres Unidas · Instrucciones R2 y pruebas de aceptación

**Candidata R2.** No reemplazar el trabajo local del equipo ni hacer commit automáticamente. Extraer este ZIP en **otra carpeta**, comparar con el commit R1 y conservar solamente cambios revisados y probados.

## 1. Rebuild en Visual Studio 2026

Abre `MujeresUnidas.slnx` y desde **Developer PowerShell** en la raíz:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Scripts\QA-Rebuild.ps1
```

Tres proyectos deben compilar sin errores. Copiar el texto completo de cualquier fallo de compilación. No publicar secretos ni datos reales.

## 2. Preparar SQL Server (solo base local de pruebas)

En SSMS, conectarse a la instancia que usa `ConnectionStrings:Main`. Asegurarse de que se trata de la **base de demostración**, sin datos de valor.

- Detener `MujeresUnidas.Server`.
- Si existe una base antigua de R1, abrir `MujeresUnidas.Server/Infrastructure/Database/910_drop_demo_database.sql`. Solo si **se autoriza eliminar TODO** el contenido de la base `MujeresUnidas`, cambiar manualmente `@Confirm` a `1` y ejecutar. Se perderán cuentas y sesiones antiguas. Si la base contiene datos importantes, **no ejecutar** y usar otra instancia de pruebas.
- Ejecutar `000_create_database.sql` y después `020_verify_fresh_demo.sql`. El script de creación es para base nueva; no actualiza estructuras incompatibles.
- Comprobar `dbo.Accounts.CommunityRole` (Member/Staff), `dbo.Accounts.StaffRole` **nullable**, `dbo.ManagementRoles`, `dbo.CommunityPosts`, `dbo.Sessions` y `dbo.AuditEvents`.
- `CommunityPosts` contiene tres entradas de ejemplo claramente etiquetadas; no son publicaciones reales.

**Si `/api/setup/status` devuelve 503:** el esquema local es anterior a R2; no intentes crear cuentas hasta corregirlo. **Si devuelve `ownerExists:true`:** ya hay un Owner en esa base, por eso la ventana de primera activación no aparece. No se desactiva con un clic: crea una base de prueba nueva si necesitas repetir el flujo inicial.

## 3. Configuración local de Server

En Developer PowerShell, situarse en `MujeresUnidas.Server` y comprobar los secretos:

```powershell
dotnet user-secrets list
```

Debe haber una conexión `ConnectionStrings:Main` válida para SQL Server y una `MU_OWNER_SETUP_KEY` de al menos 24 caracteres. Ejemplo para la conexión local (ajustar instancia):

```powershell
dotnet user-secrets set 'ConnectionStrings:Main' 'Server=localhost;Database=MujeresUnidas;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;'
```

Crear una clave fuerte aleatoria *fuera del repositorio* si aún no existe. No mandarla por chat ni ponerla en el README. En la configuración del Owner se introduce esa misma clave.

Levantar `MujeresUnidas.Server` con perfil **https**. Comprobar desde Edge:

- `https://localhost:7013/health`
- `https://localhost:7013/api/setup/status` → `{"ownerExists":false}` sobre una base nueva

También ejecutar el chequeo de lectura opcional:

```powershell
.\Scripts\QA-Smoke.ps1
```

## 4. Pruebas reales de acceso

1. **Gestión sin Owner:** debe mostrar campos Nombre, Correo, Contraseña, Clave de instalación y el botón **Configurar autoridad principal**. Todos los controles visibles incluso con escalado 200 % (registrar captura si se recortan).
2. **Owner:** con clave incorrecta no se crea; con clave correcta aparece confirmación y después el login; un segundo Owner no se puede crear. Al volver a abrir Gestión, debe ir directamente a login.
3. **Home Gestión:** iniciar sesión con Owner; comprobar contadores reales, menú Inicio/Activaciones/Citas/Personal/Casos- Seguimiento/Reportes, sin opción Publicaciones. Citas/Casos/Reportes aún muestran aviso de pendiente.
4. **Personal de Gestión:** crear una Administradora, Psicóloga, Jurídica o Trabajo Social con puesto cargado desde SQL. La Administradora puede ver activaciones; los demás puestos no reciben automáticamente esas facultades. Agregar un puesto adicional y comprobar que aparece sin recompilar.
5. **Comunidad Member:** registrar con nombre, correo ficticio, CURP ficticia de **18** caracteres, contraseña de 10+, tipo Member. Intentar login antes de aprobación: debe rechazarlo como pendiente.
6. **Gestión aprueba:** comprobar que la solicitud muestra el **tipo solicitado Member/Staff**, aprobar y volver a probar login Comunidad: debe abrir Home.
7. **Comunidad Staff:** registrar **otra** cuenta ficticia como Staff; antes de aprobar no puede iniciar sesión ni moderar. Después de aprobar, entra a Comunidad y desde Perfil puede abrir Moderación.
8. **Publicaciones:** Member crea un Blog y comprueba que está Pendiente en su perfil; no debe aparecer en Home. Staff lo ve en la cola y lo aprueba; entonces aparece bajo Blogs. Probar Rechazar, Editar y Retirar; confirmar que el servidor rechaza la aprobación de un Member.
9. **Destacados:** Inicio muestra hasta tres entradas aprobadas y destacadas; las vistas Blogs/Guías/Empleos/Eventos muestran solo su categoría. No hay opción «Todas» ni botón de más destacados.
10. **Sesiones:** cerrar sesión y verificar que `/api/auth/me` ya no acepta el token anterior. Bloquear una cuenta desde Gestión y comprobar que no entra de nuevo.
11. **Diseño:** capturas de Gestión normal y maximizada; MAUI en Windows Machine y Android; revisar que no haya controles ocultos o desplazamiento innecesario en login. Diferencias visuales se registran como Issues de QA.

## 5. Emulador Android (solo demostración con datos ficticios)

Visual Studio → proyecto `MujeresUnidas.Comunidad` → seleccionar **Android Emulator** en lugar de **Windows Machine**. Asegurarse de tener instalado workload MAUI/Android, Android SDK y una imagen de emulador. Ejecutar Server con perfil `https` en el mismo PC: además de `https://localhost:7013`, ese perfil abre **`http://localhost:5277`**. En desarrollo no se redirige este HTTP para permitir la conexión del emulador.

Android MAUI usa **`http://10.0.2.2:5277/`** (10.0.2.2 apunta al PC anfitrión del emulador). Está habilitado cleartext **solo como prototipo local con datos sintéticos**. Antes de distribuir cualquier APK o usar datos reales, cerrar HTTP, habilitar un dominio HTTPS válido y endurecer el manifiesto Android. El proyecto **no está listo para difusión a personas reales**.

## 6. Estado de la revisión y Git

`R2` contiene código fuente, no un commit. No hay migraciones. Las interfaces visuales y la base SQL requieren prueba en Windows del equipo. Una compilación correcta no sustituye pruebas reales de registro, moderación o dispositivos. Tras aprobar, revisar `git diff` antes de hacer `commit` en su repositorio privado y asociar los Issues del tablero XP.
