param(
 [Parameter(Mandatory=$true)][string]$Package,
 [Parameter(Mandatory=$true)][string]$Tag,
 [string]$SourceRoot
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if(!$SourceRoot){$SourceRoot=Split-Path $PSScriptRoot -Parent}
if($Tag -cnotmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){throw 'Expected release tag vMAJOR.MINOR.PATCH'}
$version=$Tag.Substring(1)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Package).Path)
try {
 $expected=@('manifest.json','README.md','CHANGELOG.md','icon.png','plugins/RunicStorageNetwork/RunicStorageNetwork.dll','plugins/RunicStorageNetwork/Assets/rsn_core_windows')
 $entries=[Collections.Generic.Dictionary[string,byte[]]]::new([StringComparer]::Ordinal)
 if($archive.Entries.Count -ne $expected.Count){throw 'Package must contain exactly the six release files'}
 foreach($entry in $archive.Entries){
  $name=$entry.FullName.Replace('\','/')
  if($expected -cnotcontains $name -or $entries.ContainsKey($name)){throw "Unexpected or duplicate archive entry: $name"}
  if($entry.Length -le 0 -or $entry.Length -gt 128MB){throw "Invalid file size: $name"}
  $stream=$entry.Open();$buffer=[IO.MemoryStream]::new()
  try{$stream.CopyTo($buffer);$entries.Add($name,$buffer.ToArray())}finally{$stream.Dispose();$buffer.Dispose()}
 }
 function Text([string]$name){return [Text.Encoding]::UTF8.GetString($entries[$name]).TrimStart([char]0xFEFF).Replace("`r`n","`n")}
 $manifest=(Text 'manifest.json') | ConvertFrom-Json
 $source=Get-Content -LiteralPath (Join-Path $SourceRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
 $keys=@('name','version_number','website_url','description','dependencies')
 if(Compare-Object ($manifest.PSObject.Properties.Name | Sort-Object) ($keys | Sort-Object)){throw 'Unexpected manifest fields'}
 if($manifest.name -cne 'RunicStorageNetwork' -or $manifest.version_number -cne $version){throw 'Manifest name/version does not match release'}
 foreach($key in @('name','version_number','website_url','description')){if($manifest.$key -cne $source.$key){throw "Manifest differs from source: $key"}}
 if($manifest.website_url -cne 'https://github.com/rerit33/RunicStorageNetwork' -or $manifest.description.Length -gt 250){throw 'Invalid package metadata'}
 if(($manifest.dependencies -join '|') -cne ($source.dependencies -join '|')){throw 'Dependencies differ from source'}
 foreach($pair in @(@('README.md','README.md'),@('CHANGELOG.md','CHANGELOG_EN.md'))){
  $sourceText=[IO.File]::ReadAllText((Join-Path $SourceRoot $pair[1])).TrimStart([char]0xFEFF).Replace("`r`n","`n")
  if((Text $pair[0]) -cne $sourceText){throw "Package documentation differs from source: $($pair[0])"}
 }
 if((Text 'CHANGELOG.md') -notmatch ('(?m)^## '+[regex]::Escape($version)+'\s*$') -or (Text 'CHANGELOG.md') -match '[\u0400-\u04ff]'){throw 'Expected English changelog with this version'}
 $plugin=[IO.File]::ReadAllText((Join-Path $SourceRoot 'src/Plugin.cs'))
 if($plugin -notmatch ('\[BepInPlugin\(Guid, "Runic Storage Network", "'+[regex]::Escape($version)+'"\)\]')){throw 'Plugin source version differs'}
 $assemblyInfo=[IO.File]::ReadAllText((Join-Path $SourceRoot 'src/AssemblyInfo.cs'))
 foreach($attribute in @('AssemblyVersion','AssemblyFileVersion')){
  if($assemblyInfo -notmatch ($attribute+'\("'+[regex]::Escape($version)+ '\.0"\)')){throw 'Assembly source version differs'}
 }
 $icon=$entries['icon.png']
 if($icon.Length -lt 33 -or [BitConverter]::ToString($icon,0,8) -ne '89-50-4E-47-0D-0A-1A-0A' -or [Text.Encoding]::ASCII.GetString($icon,12,4) -ne 'IHDR' -or [BitConverter]::ToString($icon,16,8) -ne '00-00-01-00-00-00-01-00'){throw 'Expected 256x256 PNG icon'}
 $bundle=$entries['plugins/RunicStorageNetwork/Assets/rsn_core_windows']
 if($bundle.Length -lt 8 -or [Text.Encoding]::ASCII.GetString($bundle,0,8) -cne "UnityFS`0"){throw 'Invalid Unity asset bundle header'}
 # Read assembly metadata without loading or executing plugin code.
 $tempFile=Join-Path ([IO.Path]::GetTempPath()) ('rsn-validation-'+[guid]::NewGuid().ToString('N')+'.dll')
 try{
  [IO.File]::WriteAllBytes($tempFile,$entries['plugins/RunicStorageNetwork/RunicStorageNetwork.dll'])
  $assembly=[Reflection.AssemblyName]::GetAssemblyName($tempFile)
  if($assembly.Name -cne 'RunicStorageNetwork' -or $assembly.Version.ToString() -cne ($version+'.0')){throw 'Compiled DLL version/name differs'}
 }finally{if(Test-Path -LiteralPath $tempFile){Remove-Item -LiteralPath $tempFile}}
 $hash=(Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash
 Write-Output "Validated RunicStorageNetwork $version; six files; SHA256=$hash"
}finally{$archive.Dispose()}
