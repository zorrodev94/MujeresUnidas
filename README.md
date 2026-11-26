# Mujeres Unidas · EarlyAccess · R2 (candidata para QA)

Solución académica del **Hackathon FIM-UAS 2026**: Comunidad (.NET MAUI), Gestión (WinForms) y Server (.NET 10 ASP.NET Core + SQL Server). Diseñada como prototipo para explorar recursos comunitarios, publicaciones moderadas y gestión de altas. **No está afiliada formalmente ni sustituye servicios de SEMUJERES, autoridades, profesionales sanitarios o emergencias.** Utilizar exclusivamente identidades y contenido ficticios durante las pruebas.

> R2 es una candidata basada en la R1 del equipo. No contiene `.git`, commits, secretos ni datos de participantes. Su compilación y funcionamiento deben validarse en Visual Studio 2026; consulta `instruccion.md`.

## Solución

| Proyecto | Responsabilidad implementada |
| --- | --- |
| `MujeresUnidas.Server` | API, validación de roles, credenciales PBKDF2, sesiones revocables, SQL parametrizado, primera cuenta Owner, personal institucional, activación de Comunidad y publicaciones moderadas |
| `MujeresUnidas.Gestion` | Primera activación del Owner, login de Owner y personal, Home con cifras reales, aprobación/bloqueo de solicitudes, alta de personal y catálogo de puestos desde SQL |
| `MujeresUnidas.Comunidad` | Login, solicitud Miembro/Staff, Home con tres destacados, vistas Blogs/Guías/Empleos/Eventos, perfil, creación/edición/retiro de publicaciones y revisión exclusiva para Staff aprobado |

`README.md`, `instruccion.md` y `LICENSE.md` están en la raíz y son *Solution Items* virtuales de Visual Studio. `Scripts/QA-Rebuild.ps1` reconstruye los tres proyectos y `Scripts/QA-Smoke.ps1` verifica respuestas HTTP de lectura sin modificar SQL.

## Operaciones de cuentas

- **OwnerUser**: única cuenta inicial, creada por clave `MU_OWNER_SETUP_KEY`; la ventana de configuración se muestra solamente cuando `ownerExists=false`. Es una clase derivada directa de `User`, no de `ManagementUser`.
- **ManagementUser**: cuenta de Gestión creada por Owner, con puestos configurables `Administradora`, `Psicóloga`, `Jurídica`, `Trabajo social`, etc. Los puestos añadidos no heredan permisos administrativos. Solamente Owner/Administradora autorizada revisa solicitudes de Comunidad.
- **CommunityMember**: solicita registro como **Member** o **Staff**, siempre `Pending` al crear la cuenta. Una cuenta Staff puede moderar publicaciones **solo después** de que un usuario autorizado la apruebe desde Gestión.
- **Separación de cuentas**: aunque dos aplicaciones tengan usuarios con el mismo nombre, no se comparten credenciales por arte de magia ni se crea automáticamente acceso cruzado. La vinculación entre Gestión y Comunidad se planificará después.

### Publicaciones

`dbo.CommunityPosts` conserva `Category`, `Title`, `Summary`, `Body`, `Status`, `AuthorId`, revisión e indicadores de destacados. Los textos **no se guardan en archivos JSON individuales**, sino en columnas SQL parametrizadas. El servidor devuelve JSON a la aplicación cliente; son formatos distintos con responsabilidades distintas. Aún **no** hay subida de imágenes de usuario: las tarjetas emplean recursos gráficos incluidos en la app. Los tres contenidos de ejemplo del SQL están señalados expresamente como ilustrativos y no son oportunidades ni eventos reales.

- `Member`: crea una publicación pendiente, la modifica (regresa a revisión), la retira, y ve sus propios estados.
- `Staff` aprobado: puede crear y editar contenido, aprobar/rechazar publicaciones pendientes y retirar publicaciones; Server comprueba siempre la autorización.
- Tipos de contenido: `Blog`, `Guide`, `Job` y `Event`; presentación en español como **Blogs, Guías, Empleos y Eventos**. En Inicio se cargan máximo **3 destacados**; no hay botón «Todas» ni «Ver más destacados».
- Longitudes: título **4–120**, resumen **8–260** y cuerpo **20–2500** caracteres. No hay búsqueda, archivos adjuntos, comentarios ni «likes» operativos en esta revisión.

## Navegación

**Gestión:** Inicio, Activaciones, Citas, Personal, Casos/Seguimiento, Reportes. Citas, Casos y Reportes están preparados como secciones pendientes; no se representan como expedientes funcionales. **Publicaciones no es un menú de Gestión.**

**Comunidad:** Inicio/Destacados y las vistas Blogs, Guías, Empleos y Eventos. Al entrar en Perfil se puede editar nombre y biografía, crear una publicación, revisar las propias y cerrar sesión. Staff aprobado ve además Moderación. Los modelos de chat, conversaciones y otros módulos permanecen pendientes.

## Seguridad y alcance

- Demo únicamente, con nombres, correos y CURP ficticios. **No capturar ni subir casos reales, testimonios identificables o información de víctimas.**
- No se envían correos, SMS, avisos a servicios gubernamentales, geolocalización ni alertas 911 desde el prototipo. El **911** se muestra solo como orientación para emergencias reales.
- Las claves del Owner y las credenciales de SQL se guardan en *User Secrets* locales. Nunca publicar los secretos en GitHub ni en capturas.
- La versión Android del repositorio usa HTTP únicamente para probar el emulador local (`10.0.2.2:5277`) en entorno Development, **no es apta para producción ni para datos reales**. Para despliegue se requerirá un endpoint HTTPS confiable y permisos Android ajustados.
- No se ejecutan migraciones ni borrados automáticos: el SQL incluye un script de restablecimiento **manual y destructivo** para una base de demostración solamente.

## Qué falta tras R2

Citas reales, expedientes y seguimiento, moderación jurídica, mensajería/chats, archivos de medios, notificaciones, roles profesionales especializados, alertas de emergencia, mapas, integración de cuentas Gestión↔Comunidad y pruebas automáticas integrales. El emulador Android, SQL Server y Windows deben someterse a QA real antes de considerarse terminados.

## Derechos

Revisar `LICENSE.md`. Las imágenes de referencia y recursos gráficos no deben presentarse como respaldo institucional oficial.
