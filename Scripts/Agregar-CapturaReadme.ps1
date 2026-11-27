[CmdletBinding()]
param([Parameter(Mandatory=$true)][ValidateSet('gestion','comunidad')][string]$Cliente,[Parameter(Mandatory=$true)][string]$ImagenPng,[switch]$DatosFicticiosConfirmados)
$ErrorActionPreference='Stop'
if(-not $DatosFicticiosConfirmados){throw 'Comprueba que no aparezcan usuarios reales, datos personales, tokens ni direcciones privadas.'}
if([IO.Path]::GetExtension($ImagenPng) -ne '.png'){throw 'Selecciona un PNG de la aplicación real.'}
$root=Split-Path -Parent $PSScriptRoot
$dest=Join-Path $root 'Assets/Capturas';New-Item -ItemType Directory -Force -Path $dest|Out-Null
Copy-Item -LiteralPath $ImagenPng -Destination (Join-Path $dest "$Cliente.png") -Force
$readme=Join-Path $root 'README.md';$raw=Get-Content -LiteralPath $readme -Raw -Encoding UTF8
$line="![Captura de $Cliente](Assets/Capturas/$Cliente.png)"
if(-not $raw.Contains($line)){[IO.File]::WriteAllText($readme,($raw.TrimEnd()+"`r`n`r`n"+$line+"`r`n"),(New-Object System.Text.UTF8Encoding($false)))}
Write-Host 'Captura incorporada. Revisa antes de commit.'
