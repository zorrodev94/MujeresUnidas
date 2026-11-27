# Compatibilidad con el comando historico; sin regeneracion ni exposicion de secretos.
# Ejecuta el flujo protegido en Preparar-Clave-Mensajes-Local.ps1.
param([switch]$NewDemoKey)
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Preparar-Clave-Mensajes-Local.ps1'
if (-not (Test-Path $script)) { throw 'Falta Preparar-Clave-Mensajes-Local.ps1.' }
if ($NewDemoKey) {
    Write-Warning 'Solo permite crear una clave nueva cuando no existen mensajes cifrados previos.'
}
& $script
if (-not $?) { throw 'Fallo la configuracion segura de mensajes.' }
