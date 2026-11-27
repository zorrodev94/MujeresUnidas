# Crear SOLO una base de demostración NUEVA. No tocar bases existentes.
[CmdletBinding()]
param(
  [string]$SqlServer='localhost',
  [switch]$ConfirmarBaseNueva
)
$ErrorActionPreference='Stop'
if (-not $ConfirmarBaseNueva) { throw 'Agrega -ConfirmarBaseNueva para confirmar una base vacía. Este script NO hace reset.' }
$sqlcmd=Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) { throw 'No se encontró sqlcmd. Instala Microsoft sqlcmd o usa SSMS siguiendo el orden de los scripts.' }
$dbDir=Join-Path (Split-Path -Parent $PSScriptRoot) 'MujeresUnidas.Server\Infrastructure\Database'
# The check must exit nonzero if a database already exists. No DROP is ever executed.
& sqlcmd -S $SqlServer -E -d master -b -Q "IF DB_ID(N'MujeresUnidas') IS NOT NULL THROW 51080, N'La base MujeresUnidas ya existe: no se modifica con este instalador.', 1;"
if($LASTEXITCODE -ne 0){throw 'La base ya existe o la conexión no está disponible. No se modificó ninguna tabla.'}
$order=@('000_create_database.sql','030_r3_messaging_profiles.sql',
  '050_r31_post_taxonomy.sql','070_r4_integracion.sql',
  '080_r42_mensajes_gestion.sql','100_r44_seguridad_y_contactos.sql',
  '120_r45_sedes_metas.sql','140_r46_momentos_comunidad.sql',
  '150_beta10_dominio.sql','155_beta10_reconciliar_sesiones.sql',
  '170_beta10_message_attachments.sql','190_beta10_encuestas.sql',
  '180_verify_beta10_full.sql','195_verify_beta10_encuestas.sql')
foreach($file in $order){
  $path=Join-Path $dbDir $file
  if(-not (Test-Path $path)){throw "Falta: $path"}
  Write-Host "Aplicando $file" -ForegroundColor Cyan
  & sqlcmd -S $SqlServer -E -d master -b -i $path
  if($LASTEXITCODE -ne 0){throw "Falló $file. Revisar el mensaje anterior; NO seguir a la siguiente migración."}
}
Write-Host 'Esquema nuevo completo. Configura los secretos FUERA de Git y prueba /health/ready.' -ForegroundColor Green
