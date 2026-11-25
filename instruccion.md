# Mujeres Unidas — instrucciones de verificación R1 y primer commit

**Estado inicial:** candidata; no aprobada hasta ejecutar estas pruebas en Windows. Usa únicamente datos sintéticos de demostración.

## 1. Compilar sin errores

En Visual Studio, abre `MujeresUnidas.slnx` y comprueba que aparecen `README.md`, `instruccion.md` y `LICENSE.md` bajo *Solution Items*. Ejecuta **Build → Rebuild Solution**. Alternativamente, en Developer PowerShell desde la raíz del repositorio:

```powershell
.\Scripts\QA-Rebuild.ps1
```

El script compila Server, Gestión y MAUI Windows por separado; falla ante un error de compilación o advertencia NuGet de seguridad `NU1902` / `NU1903`.

## 2. Restablecer el esquema experimental

Para un comienzo totalmente nuevo: detén Server; abre SSMS en la instancia local de pruebas; si ya existe una base anterior, ejecuta `910_drop_demo_database.sql` cambiando temporalmente `@Confirm` a `1`. Luego ejecuta `000_create_database.sql`. No hay datos reales que preservar en este entorno académico, y la operación es destructiva.

Después ejecuta `020_verify_fresh_demo.sql`: la columna `StaffRole` de `dbo.Accounts` debe mostrar `AllowsNull = 1`.

**Por qué:** la base utilizada durante las capturas seguía siendo de otra revisión (`StaffRole NOT NULL`), de modo que registrar Comunidad provocó SQL Server **515** y HTTP **500**. `000_create_database.sql` no reemplaza tablas existentes. El restablecimiento es explícito, destructivo y exclusivo de la base local de pruebas; nunca se ejecuta automáticamente desde la aplicación.

Verifica las tablas `dbo.Accounts`, `dbo.Sessions`, `dbo.AuditEvents` y `dbo.ManagementRoles`. El catálogo inicial contiene `Administrator`, `Psychology`, `Legal` y `SocialWork` con sus nombres visibles en español.

## 3. Pruebas funcionales en orden

1. **Server:** iniciar con perfil `https`; abrir `/health` (respuesta de proceso) y `/api/setup/status` (consulta real a SQL).
2. **Owner:** abrir Gestión, crear la cuenta única con `MU_OWNER_SETUP_KEY`. Intentar crear un segundo Owner: debe rechazarse.
3. **Gestión:** abrir `MujeresUnidas.Gestion` con Server iniciado; si no existe dueño debe verse **Activar primera cuenta** (nombre, correo, contraseña y clave); tras crearla, debe mostrarse **Iniciar sesión**. Confirmar que el Home muestra contadores y auditoría recuperados de Server, sin barras horizontales en el acceso.
4. **Comunidad:** registrar una persona ficticia. CURP de 17 y 19 caracteres: formulario rechaza; 18 caracteres alfanuméricos: se envía solicitud. Probar duplicado: respuesta 409.
5. **Pendiente:** iniciar sesión en Comunidad antes de activar: acceso denegado por cuenta pendiente.
6. **Activación:** desde Gestión, seleccionar la cuenta, activar y repetir login en Comunidad: ahora ingresa. Volver a consultar el estado desde Gestión.
7. **Perfil:** actualizar nombre y biografía desde Comunidad, cerrar y volver a abrir; deben persistir en SQL.
8. **Bloqueo:** bloquear desde Gestión, comprobar que una sesión anterior deja de autorizar nuevas solicitudes.
9. **Personal:** Owner crea una Administradora con puesto cargado del servidor; debe poder consultar y activar. Probar con Psicóloga: debe entrar a Gestión, pero no crear personal ni activar solicitudes.
10. **Puestos dinámicos:** Owner agrega un puesto nuevo desde Gestión. Debe aparecer en el ComboBox al recargar, sin recompilar. Crear personal con ese puesto; su cuenta no debe adquirir permisos de Administradora.
11. **Diseño:** comprobar MAUI Windows en ventana de simulación telefónica (aprox. 430 × 700); el acceso muestra logo compacto, correo, contraseña y ambos botones **sin obligar a bajar por la ilustración**. En registro puede ser necesario desplazarse por los cuatro campos. Comprobar WinForms a 100 %, 150 % y 200 % de escala; registrar cualquier recorte, sobreposición o texto oculto.
12. **Seguridad:** confirmar que ningún secreto está en el repositorio; revisar *Dependencies → NuGet* y que no persistan advertencias `NU1902` / `NU1903`.

## 4. Criterio para el primer commit

**No realizar el commit** si falla cualquier compilación o una operación central de login, registro o activación. Si falla una prueba, guardar el número de error, archivo/línea y el texto de salida en Visual Studio, sin publicar contraseñas, claves o CURP reales.

Cuando todo pase:

```powershell
git status
git add .
git diff --cached --check
git diff --cached --stat
git commit -m "feat: primera revision funcional Mujeres Unidas R1"
```

El commit lo realiza el responsable del proyecto, desde su repositorio local después de validar. Este paquete **no contiene un repositorio `.git` ni un commit prefabricado**.
