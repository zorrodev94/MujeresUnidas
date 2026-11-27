[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Publicacion,[Parameter(Mandatory=$true)][string]$SalidaAnterior,[switch]$UsarSecretosDesarrollo,[switch]$ConservarMediaNueva)
$ErrorActionPreference='Stop'
$dir=(Resolve-Path -LiteralPath $Publicacion).Path
$old=(Resolve-Path -LiteralPath $SalidaAnterior).Path
$dll=Join-Path $dir 'MujeresUnidas.Server.dll'
if(-not (Test-Path -LiteralPath $dll)){throw 'La publicación no contiene MujeresUnidas.Server.dll.'}
# Keep the existing settings and key values, never output them.
$names=@('ConnectionStrings__Main','MU_OWNER_SETUP_KEY','MU_MESSAGE_KEY_BASE64','MU_CONTACT_KEY_BASE64','MU_MEDIA_ROOT','MU_POST_MEDIA_ROOT')
$before=@{};foreach($name in $names+@('ASPNETCORE_ENVIRONMENT','ASPNETCORE_URLS','DOTNET_ENVIRONMENT')){$before[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try {
 if($UsarSecretosDesarrollo){
   $secretFile=Join-Path $env:APPDATA 'Microsoft/UserSecrets/b5ba3a4a-3355-4bdc-bde1-b9696ec27096/secrets.json'
   if(-not(Test-Path -LiteralPath $secretFile)){throw 'No se encontraron los User Secrets originales de esta cuenta Windows.'}
   $secret=Get-Content -LiteralPath $secretFile -Raw|ConvertFrom-Json
   foreach($name in $names){
     $key=$name.Replace('__',':');$entry=$secret.PSObject.Properties[$key]
     if($entry -and -not [Environment]::GetEnvironmentVariable($name,'Process')){[Environment]::SetEnvironmentVariable($name,[string]$entry.Value,'Process')}
   }
   if(-not $env:ConnectionStrings__Main -and $secret.ConnectionStrings){$env:ConnectionStrings__Main=$secret.ConnectionStrings.Main}
 }
 foreach($name in @('ConnectionStrings__Main','MU_MESSAGE_KEY_BASE64','MU_CONTACT_KEY_BASE64')){
   if(-not [Environment]::GetEnvironmentVariable($name,'Process')){throw "Falta $name. Conserva la configuración anterior; no generes claves nuevas."}
 }
 foreach($name in @('MU_MESSAGE_KEY_BASE64','MU_CONTACT_KEY_BASE64')){if(([Convert]::FromBase64String([Environment]::GetEnvironmentVariable($name,'Process'))).Length -ne 32){throw "Clave $name no válida."}}
 # Default storage in the previous executable directory must stay available after switching builds.
 # If existing MU_* roots are already configured they are left untouched.
 # Default attachments and profiles use different sibling folders: copy the tree instead of remapping them.
 $oldMedia=Join-Path $old 'PrivateMedia';$newMedia=Join-Path $dir 'PrivateMedia'
 if($old -ne $dir -and (Test-Path -LiteralPath $oldMedia)){
   if(Test-Path -LiteralPath $newMedia){
     if(-not $ConservarMediaNueva){throw 'PrivateMedia existe en ambos despliegues. Si esta es la publicación que ya estaba activa, reiníciala con -ConservarMediaNueva; nunca sobrescribas archivos.'}
   } else {Copy-Item -LiteralPath $oldMedia -Destination $newMedia -Recurse}
 }
 $env:ASPNETCORE_ENVIRONMENT='Production';$env:DOTNET_ENVIRONMENT='Production';$env:ASPNETCORE_URLS='http://localhost:5277'
 Push-Location $dir
 try {& dotnet $dll; if($LASTEXITCODE -ne 0){throw 'Server terminó con error.'}} finally {Pop-Location}
} finally {foreach($name in $before.Keys){[Environment]::SetEnvironmentVariable($name,$before[$name],'Process')}}
