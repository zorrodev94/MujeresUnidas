[CmdletBinding()]
param([Parameter(Mandatory=$true)][ValidateSet('Server','Gestion','ComunidadAndroid','ComunidadWindows')][string]$Componente,
      [string]$ApiHttps,[string]$Salida)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Salida){$Salida=Join-Path $root ("artifacts/"+$Componente)}
$Salida=[IO.Path]::GetFullPath($Salida)
if(Test-Path $Salida){if(@(Get-ChildItem -LiteralPath $Salida -Force).Count -gt 0){throw 'La carpeta de salida debe estar vacía. Usa una ruta nueva; no sobrescribas un despliegue activo.'}}
$config=$null;$previous=$null
try {
 switch($Componente){
 'Server' {$project='MujeresUnidas.Server';$extra=@('-f','net10.0','--self-contained','false')}
 'Gestion' {$project='MujeresUnidas.Gestion';$extra=@('-f','net10.0-windows','-r','win-x64','--self-contained','true');$config=Join-Path $root "$project/server-endpoint.json"}
 'ComunidadAndroid' {$project='MujeresUnidas.Comunidad';$extra=@('-f','net10.0-android','-p:AndroidPackageFormats=apk');$config=Join-Path $root "$project/Resources/Raw/server-endpoint.json"}
 'ComunidadWindows' {$project='MujeresUnidas.Comunidad';$extra=@('-f','net10.0-windows10.0.19041.0','-p:WindowsPackageType=None','-p:WindowsAppSDKSelfContained=true','-p:RuntimeIdentifierOverride=win-x64','--self-contained','true');$config=Join-Path $root "$project/Resources/Raw/server-endpoint.json"}
 }
 if($config){
   $uri=$null
   if(-not [Uri]::TryCreate($ApiHttps,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -ne 'https' -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/'){throw 'Indica -ApiHttps con la URL base HTTPS, sin rutas, credenciales ni parámetros.'}
   $ready=Invoke-RestMethod ($uri.AbsoluteUri.TrimEnd('/')+'/health/ready') -TimeoutSec 15
   if($ready.status -ne 'ready' -or $ready.version -ne '1.0.0'){throw 'La API debe estar en versión 1.0.0 y responder ready antes de publicar clientes.'}
   $previous=[IO.File]::ReadAllBytes($config)
   $json=@{apiBaseUrl=$uri.AbsoluteUri.TrimEnd('/')+'/'}|ConvertTo-Json
   [IO.File]::WriteAllText($config,$json,(New-Object System.Text.UTF8Encoding($false)))
 }
 $csproj=Join-Path $root "$project/$project.csproj"
 & dotnet publish $csproj -c Release -o $Salida @extra
 if($LASTEXITCODE -ne 0){throw "Falló publish de $Componente."}
 if($Componente -eq 'ComunidadAndroid'){
   $apks=@(Get-ChildItem -LiteralPath $Salida -Recurse -Filter '*.apk');if($apks.Count -eq 0){throw 'Publish no generó APK en la salida. Revisa firma y artefactos.'}
   $apks|Select-Object Name,FullName
 }
 Write-Host "Publish generado: $Salida. Falta prueba de instalación/ejecución." -ForegroundColor Green
} finally {if($config -and $null -ne $previous){[IO.File]::WriteAllBytes($config,$previous)}}
