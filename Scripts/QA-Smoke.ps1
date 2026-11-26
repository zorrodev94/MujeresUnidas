# Read-only R2 API preflight. Never sends credentials or writes to SQL.
$ErrorActionPreference='Stop'
$base='https://localhost:7013'
try {
    $health=Invoke-RestMethod "$base/health" -TimeoutSec 8
    Write-Host "Health: $($health.status)" -ForegroundColor Green
    $setup=Invoke-RestMethod "$base/api/setup/status" -TimeoutSec 8
    Write-Host "Owner exists: $($setup.ownerExists)" -ForegroundColor Cyan
    Write-Host 'La comprobación no crea ni elimina cuentas.' -ForegroundColor Green
} catch {
    Write-Error "R2 preflight no completado: $($_.Exception.Message). Comprueba el Server, HTTPS y el esquema SQL."
    exit 1
}
