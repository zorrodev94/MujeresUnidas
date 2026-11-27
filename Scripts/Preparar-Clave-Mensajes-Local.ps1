# EarlyAccess R4.3: DO NOT RUN AGAINST PRODUCTION WITHOUT BACKUP
# This script never overwrites existing secrets. Run on the machine hosting Server.
$ErrorActionPreference = 'Stop'
$server = Join-Path $PSScriptRoot '..\MujeresUnidas.Server\MujeresUnidas.Server.csproj'
if (-not (Test-Path $server)) { throw 'No se encontró MujeresUnidas.Server.csproj.' }
if (-not [string]::IsNullOrWhiteSpace($env:MU_MESSAGE_KEY_BASE64)) {
    Write-Host 'Existe una clave en la sesión; no se modifica.'
    exit 0
}
$all = & dotnet user-secrets list --project $server 2>&1
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar User Secrets. Revisa el SDK de .NET y UserSecretsId.' }
if (($all | Out-String) -match '(?m)^\s*MU_MESSAGE_KEY_BASE64\s*=') {
    Write-Host 'Ya existe una clave de mensajería en User Secrets: se conserva sin cambios.'
    exit 0
}
Write-Host 'No hay clave almacenada en este entorno. Si existen mensajes anteriores, RECUPERA LA CLAVE ORIGINAL y no generes otra.'
$answer = Read-Host 'Es un laboratorio nuevo, sin mensajes cifrados anteriores? Escribe CREAR para continuar'
if ($answer -cne 'CREAR') { Write-Host 'No se realizaron cambios.'; exit 0 }
$buffer = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($buffer) } finally { $rng.Dispose() }
$secret = [System.Convert]::ToBase64String($buffer)
& dotnet user-secrets set MU_MESSAGE_KEY_BASE64 $secret --project $server | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'No se pudo escribir el secreto.' }
Write-Host 'Clave local almacenada en User Secrets. No se imprime ni se empaqueta.'
Write-Host 'Conserva una copia privada de tus secretos en un gestor seguro y reinicia Server.'
