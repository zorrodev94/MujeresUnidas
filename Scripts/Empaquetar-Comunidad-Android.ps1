[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ApiHttps,[string]$Salida)
& (Join-Path $PSScriptRoot 'Publicar-Final.ps1') -Componente ComunidadAndroid -ApiHttps $ApiHttps -Salida $Salida
