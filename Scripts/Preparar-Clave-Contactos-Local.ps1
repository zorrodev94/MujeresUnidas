# Solo para laboratorio LOCAL. No regenera claves existentes ni imprime secretos.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\MujeresUnidas.Server\MujeresUnidas.Server.csproj'
if (-not (Test-Path $project)) { throw 'No se encontró el proyecto Server.' }
if (-not [string]::IsNullOrWhiteSpace($env:MU_CONTACT_KEY_BASE64)) {
    Write-Host 'Existe MU_CONTACT_KEY_BASE64 en el entorno: conserva la original.'
    exit 0
}
$entries = (& dotnet user-secrets list --project $project 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar User Secrets.' }
if ($entries -match '(?m)^\s*MU_CONTACT_KEY_BASE64\s*[:=]') {
    Write-Host 'Ya existe una clave de contactos en User Secrets. No se modificó.'
    exit 0
}
Write-Warning 'Crea esta clave solo si NUNCA se guardaron contactos cifrados con otra clave.'
$reply = Read-Host '¿Crear una clave NUEVA para una base local nueva? (SI para continuar)'
if ($reply -ne 'SI') { Write-Host 'Cancelado'; exit 0 }
# Compatible con Windows PowerShell 5.1 y PowerShell 7.
$buffer = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $rng.GetBytes($buffer)
} finally {
    $rng.Dispose()
}
$key = [Convert]::ToBase64String($buffer)
& dotnet user-secrets set MU_CONTACT_KEY_BASE64 $key --project $project | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'No fue posible guardar la clave.' }
Remove-Variable key,buffer -ErrorAction SilentlyContinue
Write-Host 'Clave creada en User Secrets. Reinicia Server. No compartas la clave.'
