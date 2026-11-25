# QA-R1 - Build Server, Gestión and Comunidad Windows before approving a commit.
# Does not modify SQL, Git history or local secrets.
$ErrorActionPreference = 'Stop'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'No se encontró el SDK dotnet. Instala .NET 10 y las cargas de trabajo de Visual Studio.'
}
$root = Split-Path -Parent $PSScriptRoot
$projects = @(
    @{ Path = 'MujeresUnidas.Server/MujeresUnidas.Server.csproj'; Framework = $null },
    @{ Path = 'MujeresUnidas.Gestion/MujeresUnidas.Gestion.csproj'; Framework = $null },
    @{ Path = 'MujeresUnidas.Comunidad/MujeresUnidas.Comunidad.csproj'; Framework = 'net10.0-windows10.0.19041.0' }
)
foreach ($entry in $projects) {
    $file = Join-Path $root $entry.Path
    if (-not (Test-Path $file)) { throw "No existe el proyecto: $file" }
    Write-Host "Rebuilding: $($entry.Path)" -ForegroundColor Cyan
    $args = @('build', $file, '-c', 'Debug', '-t:Rebuild', '-warnaserror:NU1902,NU1903')
    if ($entry.Framework) { $args += @('-f', $entry.Framework) }
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "Rebuild falló: $($entry.Path). Revisa la salida de MSBuild." }
}
Write-Host 'Compilación completada. Continuar con las pruebas funcionales de instruccion.md.' -ForegroundColor Green
