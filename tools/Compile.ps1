param(
 [string]$Output,
 [switch]$Tests,
 [string]$EditorData,
 [string]$ValheimManaged,
 [string]$BepInExPath
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$localConfig=Join-Path $root '.local\BuildPaths.psd1'
$paths=@{}
if(Test-Path -LiteralPath $localConfig){$paths=Import-PowerShellDataFile -LiteralPath $localConfig}
if(!$EditorData){$EditorData=$paths['EditorData']}
if(!$ValheimManaged){$ValheimManaged=$paths['ValheimManaged']}
if(!$BepInExPath){$BepInExPath=$paths['BepInExPath']}
if(!$EditorData){throw 'Pass -EditorData pointing to Unity 6000.0.75f1 Editor\Data. See BUILDING.md.'}
if(!$Tests -and (!$ValheimManaged -or !$BepInExPath)){throw 'Pass -ValheimManaged and -BepInExPath. See BUILDING.md.'}
$editor=$EditorData
$game=$ValheimManaged
$profile=$BepInExPath
foreach($required in @('MonoBleedingEdge\lib\mono\4.7.2-api','NetCoreRuntime\dotnet.exe','DotNetSdkRoslyn\csc.dll')){
 if(!(Test-Path -LiteralPath (Join-Path $editor $required))){throw "Unity compiler component missing: $required"}
}
if(!$Output){$Output=Join-Path $root 'artifacts\compile'}
New-Item -ItemType Directory -Force $Output | Out-Null
$refs=@(Get-ChildItem "$editor\MonoBleedingEdge\lib\mono\4.7.2-api" -Filter '*.dll' | ForEach-Object FullName)
$refs+=@(Get-ChildItem "$editor\MonoBleedingEdge\lib\mono\4.7.2-api\Facades" -Filter '*.dll' | ForEach-Object FullName)
if($Tests){$files=@((Join-Path $root 'src\Planner.cs'),(Join-Path $root 'src\NetworkGraph.cs'),(Join-Path $root 'src\TranslationCatalog.cs'),(Join-Path $root 'src\IncrementalCount.cs'),(Join-Path $root 'src\SourceGate.cs'),(Join-Path $root 'src\Recovery.cs'),(Join-Path $root 'src\StockCatalog.cs'),(Join-Path $root 'src\ContainerRules.cs'),(Join-Path $root 'src\BuildToolRules.cs'),(Join-Path $root 'src\NameIndex.cs'),(Join-Path $root 'src\NetworkLabels.cs'))+@(Get-ChildItem "$root\tests" -Filter '*.cs' | ForEach-Object FullName);$target='exe';$name='PlannerTests.exe'}else{
 $refs+=@(Get-ChildItem $game -Filter 'Unity*.dll' | ForEach-Object FullName)
 $refs+=@("$game\assembly_valheim.dll","$game\assembly_utils.dll","$game\assembly_guiutils.dll","$game\Assembly-CSharp.dll","$game\Splatform.dll","$game\gui_framework.dll","$game\SoftReferenceableAssets.dll","$profile\core\BepInEx.dll","$profile\core\0Harmony.dll","$profile\plugins\ValheimModding-Jotunn\Jotunn.dll")
 $files=@(Get-ChildItem "$root\src" -Filter '*.cs' | ForEach-Object FullName);$target='library';$name='RunicStorageNetwork.dll'
}
foreach($ref in $refs){if(!(Test-Path -LiteralPath $ref)){throw "Missing reference $ref"}}
$rsp=Join-Path $Output ($name+'.rsp')
$lines=@('/nologo','/nostdlib+','/langversion:9','/deterministic+','/optimize+','/warn:4',"/target:$target",('/out:"'+(Join-Path $Output $name)+'"'))
$lines+=@($refs | Select-Object -Unique | ForEach-Object {'/reference:"'+$_+'"'})
$lines+=@($files | ForEach-Object {'"'+$_+'"'})
[IO.File]::WriteAllLines($rsp,$lines)
& "$editor\NetCoreRuntime\dotnet.exe" "$editor\DotNetSdkRoslyn\csc.dll" "@$rsp"
if($LASTEXITCODE -ne 0){throw "C# compiler failed: $LASTEXITCODE"}
if($Tests){& (Join-Path $Output $name);if($LASTEXITCODE -ne 0){throw 'Isolated tests failed'}}
if($Tests){
 # Compile the production policy and exact build-entry methods against game stand-ins.
 # No game assemblies are loaded, and the fixture is excluded from the regular suite.
 function Read-TestMethod([string]$File,[string]$Declaration){
  $source=[IO.File]::ReadAllText((Join-Path $root $File))
  $match=[regex]::Match($source,'(?ms)^  '+[regex]::Escape($Declaration)+'\(.*?^  }')
  if(!$match.Success){throw "Build test method not found: $Declaration"}
  return $match.Value
 }
 $methods=@('internal static PieceTable BuildTable','internal static bool BuildPiece','internal static bool LocalBuildMaterials','internal static bool BuildToolUsable','internal static bool Build') | ForEach-Object {Read-TestMethod 'src\Actions.cs' $_}
 $have=Read-TestMethod 'src\Patches.cs' 'static bool HaveBuild'
 $extracted=Join-Path $Output 'BuildEntryMethods.cs'
 [IO.File]::WriteAllText($extracted,"using System; using System.Linq; using System.Collections.Generic; using UnityEngine; using RunicStorageNetwork.Logic;`nnamespace RunicStorageNetwork { internal static partial class Actions {`n"+($methods -join "`n")+"`n} internal static partial class Patches {`n"+$have+"`n} }")
 $probe=Join-Path $Output 'BuildToolRuntimeTests.exe'
 $probeRsp=Join-Path $Output 'BuildToolRuntimeTests.rsp'
 $probeLines=@('/nologo','/nostdlib+','/langversion:9','/target:exe','/define:BUILD_TOOL_RUNTIME_TESTS',('/out:"'+$probe+'"'))
 $probeLines+=@($refs | Select-Object -Unique | ForEach-Object {'/reference:"'+$_+'"'})
 $probeLines+=@('src\BuildToolPolicy.cs','src\BuildToolRules.cs','src\ContainerRules.cs','src\Planner.cs','tests\BuildToolRuntimeTests.cs' | ForEach-Object {'"'+(Join-Path $root $_)+'"'})
 $probeLines+='"'+$extracted+'"'
 [IO.File]::WriteAllLines($probeRsp,$probeLines)
 & "$editor\NetCoreRuntime\dotnet.exe" "$editor\DotNetSdkRoslyn\csc.dll" "@$probeRsp"
 if($LASTEXITCODE -ne 0){throw 'Build runtime test compilation failed'}
 & $probe
 if($LASTEXITCODE -ne 0){throw 'Build runtime tests failed'}

 # Exercise the real recipe index and transaction requirement selection together.
 $recipeMethods=@('internal bool ReadRequirements','internal bool SelectNeeds') | ForEach-Object {Read-TestMethod 'src\Transport.cs' $_}
 $stockMethods=@('internal static List<Need> Requirements','internal static bool Qualities') | ForEach-Object {Read-TestMethod 'src\Core.cs' $_}
 $recipeExtracted=Join-Path $Output 'RecipeEntryMethods.cs'
 [IO.File]::WriteAllText($recipeExtracted,"using System; using System.Linq; using System.Collections.Generic; using UnityEngine; using RunicStorageNetwork.Logic;`nnamespace RunicStorageNetwork { internal sealed partial class Operation {`n"+($recipeMethods -join "`n")+"`n} internal static partial class Stockroom {`n"+($stockMethods -join "`n")+"`n} }")
 $recipeProbe=Join-Path $Output 'RecipeRuntimeTests.exe'
 $recipeRsp=Join-Path $Output 'RecipeRuntimeTests.rsp'
 $recipeLines=@('/nologo','/nostdlib+','/langversion:9','/target:exe','/define:RECIPE_RUNTIME_TESTS',('/out:"'+$recipeProbe+'"'))
 $recipeLines+=@($refs | Select-Object -Unique | ForEach-Object {'/reference:"'+$_+'"'})
 $recipeLines+=@('src\RecipeIndex.cs','src\NameIndex.cs','src\Planner.cs','tests\RecipeRuntimeTests.cs' | ForEach-Object {'"'+(Join-Path $root $_)+'"'})
 $recipeLines+='"'+$recipeExtracted+'"'
 [IO.File]::WriteAllLines($recipeRsp,$recipeLines)
 & "$editor\NetCoreRuntime\dotnet.exe" "$editor\DotNetSdkRoslyn\csc.dll" "@$recipeRsp"
 if($LASTEXITCODE -ne 0){throw 'Recipe runtime test compilation failed'}
 & $recipeProbe
 if($LASTEXITCODE -ne 0){throw 'Recipe runtime tests failed'}
}
