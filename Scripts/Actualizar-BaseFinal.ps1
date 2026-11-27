[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$SqlServer,[switch]$RespaldoConfirmado,[switch]$AplicarEncuesta)
$ErrorActionPreference='Stop'
if(-not $RespaldoConfirmado){throw 'Respalda la base existente y detén SOLO la API. Luego agrega -RespaldoConfirmado.'}
if(-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)){throw 'Instala sqlcmd o usa SSMS con los scripts indicados en instruccion.md.'}
$dbDir=Join-Path (Split-Path -Parent $PSScriptRoot) 'MujeresUnidas.Server/Infrastructure/Database'
$order=@('180_verify_beta10_full.sql')
if($AplicarEncuesta){$order+=@('190_beta10_encuestas.sql')}
$order+=@('195_verify_beta10_encuestas.sql')
foreach($name in $order){
  & sqlcmd -S $SqlServer -E -d MujeresUnidas -b -i (Join-Path $dbDir $name)
  if($LASTEXITCODE -ne 0){throw "Falló $name. No iniciar el nuevo servidor hasta resolverlo."}
}
Write-Host 'Esquema comprobado. Sin DROP ni reinicio de SQL.' -ForegroundColor Green
