# Mujeres Unidas 1.0.0 · Instalación y entrega

## 1. Ryzen: compilar y revisar Git

Extrae la solución en una carpeta nueva. No reemplaces `.git`, secretos, bases ni archivos privados. Desde la raíz:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\Scripts\QA-Rebuild.ps1
```

Si falla, no publiques. Revisa el código de error. Esta entrega no fue compilada en Windows/MAUI durante su preparación.

Integra el contenido revisado en el clon privado existente (no copies bin, obj ni artifacts). Comprueba diferencias, elimina los Docs históricos si todavía están en el clon, no borres archivos ajenos y conserva el historial. Ejemplo, **desde el clon correcto**:

```powershell
git status --short
git add -A
git diff --cached --check
git diff --cached --stat
git commit -m "release: Mujeres Unidas 1.0.0 Hackathon EarlyAccess"
git push origin HEAD
```

Revisa el diff completo y secretos antes del commit. No uses force push. Comprueba la rama y política del repositorio; si hace falta, utiliza PR. El ZIP no contiene un commit remoto.

## 2. ProLiant: actualizar sin destruir datos

1. Haz copia de seguridad de la base `MujeresUnidas`, configuración original, claves y archivos `PrivateMedia`. Anota el directorio **real** del DLL que ejecutaba la API anterior y rutas MU_MEDIA_ROOT / MU_POST_MEDIA_ROOT si existen. No regeneres claves; se perdería acceso a contenido cifrado.
2. Copia el código en una carpeta nueva. Publica Server en una salida vacía:

```powershell
.\Scripts\Publicar-Final.ps1 -Componente Server
```

3. Detén **solo Kestrel de Mujeres Unidas**, no SQL, Cloudflare ni la web principal. Verifica la base existente con SSMS: ejecuta `180_verify_beta10_full.sql`, `190_beta10_encuestas.sql` (idempotente), `195_verify_beta10_encuestas.sql` en ese orden. Archivos en `MujeresUnidas.Server/Infrastructure/Database`. Alternativa con autenticación Windows y sqlcmd:

```powershell
$sql = Read-Host 'Instancia SQL existente'
.\Scripts\Actualizar-BaseFinal.ps1 -SqlServer $sql -RespaldoConfirmado -AplicarEncuesta
```

Si la verificación 180 falla, identifica la migración previa faltante; no ejecutes un reset. Instalación desde cero es un escenario aparte: `Inicializar-Beta10-BaseNueva.ps1` se niega si la base existe.

4. Arranca el nuevo Server conservando las configuraciones originales. Si usabas variables de entorno, reutilízalas (sin imprimir secretos). Si usabas User Secrets con la misma cuenta de Windows, agrega `-UsarSecretosDesarrollo` para importarlos únicamente a este proceso de producción:

```powershell
$old = Read-Host 'Carpeta del DLL anterior (contiene PrivateMedia si no hay rutas externas)'
.\Scripts\Iniciar-ServerFinal.ps1 -Publicacion '.\artifacts\Server' -SalidaAnterior $old -UsarSecretosDesarrollo
```

El script requiere la conexión y las dos claves de cifrado existentes. Si el despliegue anterior usaba appsettings local o cuenta de servicio distinta, vuelve a cargar sus valores seguros como variables de entorno antes de ejecutar, no copies secretos al repo. Conserva `MU_OWNER_SETUP_KEY` si aún no existe Owner. El script copia `PrivateMedia` anterior a una salida nueva si existe; no sustituye un respaldo. En reinicios posteriores de la misma publicación, agrega `-ConservarMediaNueva` únicamente si esa carpeta contiene los datos actuales; no copies de nuevo la antigua. Mantén la consola activa. Si falla el arranque antes de admitir usuarios, detén el nuevo proceso y recupera el anterior; no borres datos. Si ya recibió escrituras, reconcilia la media antes de revertir. Para producción real se requiere un servicio y almacenamiento persistente administrado.

5. La API escucha `http://localhost:5277`. El túnel remoto ya configurado debe seguir apuntando a ese puerto; no cambies DNS ni rutas. En otra consola:

```powershell
.\Scripts\QA-Smoke.ps1 -ApiBase 'http://localhost:5277'
$api = Read-Host 'URL pública HTTPS base'
.\Scripts\QA-Smoke.ps1 -ApiBase $api
```

Resultado esperado: `status=ready`, `schema=beta10` (compatibilidad SQL), `version=1.0.0`. Prueba desde otra red. Si aparece el resultado anterior sin `version`, aún está ejecutándose el Server anterior. Si da 503 revisa SQL/configuración. Nunca desactives certificados ni expongas SQL, RDP o iLO.

## 3. Publicar clientes desde Ryzen

Los archivos endpoint vienen vacíos; los valores locales de desarrollo no sirven para los teléfonos. Configura la URL pública al publicar, sin `/health/ready` ni puerto interno. El script valida la API y restaura el archivo de configuración del código después de cada publish.

```powershell
$api = Read-Host 'URL pública HTTPS base'
.\Scripts\Publicar-Final.ps1 -Componente Gestion -ApiHttps $api
.\Scripts\Publicar-Final.ps1 -Componente ComunidadAndroid -ApiHttps $api
# Opcional: Comunidad en Windows
.\Scripts\Publicar-Final.ps1 -Componente ComunidadWindows -ApiHttps $api
```

Distribuye la carpeta de publicación de Gestión completa, no solo su EXE. En Android comprueba el APK firmado, instalación y actualización (si cambió la firma, Android puede exigir desinstalar la versión anterior; respalda datos locales antes). El script no crea certificados de firma privados ni garantiza publicación en tiendas. En caso de error de MAUI, confirma workloads y SDK Android. Las publicaciones permanecen fuera de Git.

## 4. Prueba de aceptación (datos ficticios)

- Gestión: Owner existente, alta Staff, roles, activación/bloqueo, vista de reportes y exportación CSV.
- Comunidad: registro, aprobación desde Gestión, login, perfil, publicación y moderación; dos cuentas para mensaje y permisos.
- Atención: crea caso de demostración, asigna sede compatible, cita y consulta su estado; verificar que el informe anual cuenta citas y casos por separado.
- Croquis: selecciona sede, vincula sector y actualiza. Comprueba que cambia el conteo y se conserva tras reinicio. No hay mapa de calles ni coordenadas de usuarias.
- Encuesta: enviar con consentimiento, editar, retirar. Si no se conoce mes de inicio, no marcarlo. Menos de cinco participantes no habilitan agregado; valores o complementos menores a cinco se reservan (—). La cifra corresponde a respuestas actualizadas en el año, no a una serie histórica inmutable ni prueba de impacto causal.
- Metas: registrar objetivo y evaluación con fuente; no simular resultados de éxito. Verifica actualización de la sección de encuestas.
- Seguridad: verificar rechazo de token de otra aplicación, acceso a mensajes ajenos, contenido privado y recursos institucionales sin permisos. No usar datos reales.
- UI: comprobar 1366x768, escalado 100/150/200%, móvil real, navegación, botones, campos y scroll.

## 5. Capturas y video

Para el README utiliza capturas de ejecución real, nunca imágenes conceptuales presentadas como evidencia. Guarda PNG sin información sensible y ejecuta:

```powershell
.\Scripts\Agregar-CapturaReadme.ps1 -Cliente gestion -ImagenPng (Read-Host 'Ruta PNG') -DatosFicticiosConfirmados
.\Scripts\Agregar-CapturaReadme.ps1 -Cliente comunidad -ImagenPng (Read-Host 'Ruta PNG') -DatosFicticiosConfirmados
```

Haz un segundo commit solo de las capturas revisadas. Graba una demostración breve: acceso, publicación comunitaria, aprobación, cita/sede, croquis, encuesta y reporte. Muestra solo operaciones que funcionen. El agente IA quedó fuera; no hay atención oficial ni emergencia real.
