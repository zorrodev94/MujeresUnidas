[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ApiBase)
$ErrorActionPreference='Stop'
$base=$ApiBase.TrimEnd('/')
$health=Invoke-RestMethod "$base/health" -TimeoutSec 15
$ready=Invoke-RestMethod "$base/health/ready" -TimeoutSec 15
if($health.status -ne 'alive' -or $ready.status -ne 'ready' -or $ready.version -ne '1.0.0'){throw 'API no lista o versión incorrecta.'}
$setup=Invoke-RestMethod "$base/api/setup/status" -TimeoutSec 15
Write-Host "API lista: versión $($ready.version), esquema $($ready.schema), Owner existe: $($setup.ownerExists)" -ForegroundColor Green
Write-Host 'Solo preflight; no sustituye inicio de sesión, permisos ni pruebas de ambos clientes.'
