param(
 [Parameter(Mandatory=$true)][string]$Package,
 [Parameter(Mandatory=$true)][string]$Tag
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$validator=Join-Path $PSScriptRoot 'ValidateRelease.ps1'
& $validator -Package $Package -Tag $Tag -SourceRoot $root | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$directory=Join-Path $root ('artifacts/release-validation-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$cases=@(
 @{Name='extra-file'; Entry='private.log'; Value='must not ship'},
 @{Name='stale-readme'; Entry='README.md'; Value='outdated documentation'},
 @{Name='stale-changelog'; Entry='CHANGELOG.md'; Value='outdated changes'},
 @{Name='invalid-icon'; Entry='icon.png'; Value='not a PNG'},
 @{Name='invalid-bundle'; Entry='plugins/RunicStorageNetwork/Assets/rsn_core_windows'; Value='not a bundle'},
 @{Name='invalid-dll'; Entry='plugins/RunicStorageNetwork/RunicStorageNetwork.dll'; Value='not a DLL'},
 @{Name='wrong-version'; Entry='manifest.json'; Value=((Get-Content (Join-Path $root 'manifest.json') -Raw) -replace '"version_number"\s*:\s*"[^"]+"','"version_number": "999.0.0"')}
)
foreach($case in $cases){
 $copy=Join-Path $directory ($case.Name+'.zip');Copy-Item -LiteralPath $Package -Destination $copy
 $zip=[IO.Compression.ZipFile]::Open($copy,[IO.Compression.ZipArchiveMode]::Update)
 try{
  $old=@($zip.Entries | Where-Object {$_.FullName.Replace('\','/') -ceq $case.Entry})
  foreach($entry in $old){$entry.Delete()}
  $entry=$zip.CreateEntry($case.Entry);$stream=$entry.Open()
  try{$bytes=[Text.Encoding]::UTF8.GetBytes($case.Value);$stream.Write($bytes,0,$bytes.Length)}finally{$stream.Dispose()}
 }finally{$zip.Dispose()}
 $rejected=$false
 try{& $validator -Package $copy -Tag $Tag -SourceRoot $root | Out-Null}catch{$rejected=$true}
 if(!$rejected){throw "Invalid package accepted: $($case.Name)"}
 Write-Output "PASS rejected $($case.Name)"
}
$rejected=$false
try{& $validator -Package $Package -Tag 'v0.0.0;invalid' -SourceRoot $root | Out-Null}catch{$rejected=$true}
if(!$rejected){throw 'Invalid tag accepted'}
Write-Output 'PASS rejected invalid tag; 8 rejection checks passed.'
