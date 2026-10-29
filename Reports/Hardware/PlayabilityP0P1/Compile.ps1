param([string[]]$Assemblies=@('AssemblyZero.Domain','AssemblyZero.Unity','AssemblyZero.Tests','AssemblyZero.PlayModeTests','AssemblyZero.Unity.Editor'),[string]$EditorData='D:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Data')
# Offline compile with Unity's Roslyn and the existing Bee response files; outputs stay under Temp/P0P1.
# New source files that Unity has not yet imported are appended automatically.
$ErrorActionPreference='Stop'
Set-Location -LiteralPath (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$s='Temp/P0P1';New-Item -ItemType Directory $s -Force | Out-Null
$roots=@{'AssemblyZero.Domain'='Assets/_AssemblyZero/Domain';'AssemblyZero.Unity'='Assets/_AssemblyZero/Runtime';'AssemblyZero.Tests'='Assets/_AssemblyZero/Tests/EditMode';'AssemblyZero.PlayModeTests'='Assets/_AssemblyZero/Tests/PlayMode';'AssemblyZero.Unity.Editor'='Assets/_AssemblyZero/Editor'}
foreach($a in $Assemblies){
  $r=Get-Content "Library/Bee/artifacts/1900b0aE.dag/$a.rsp"
  $r=$r -replace 'Library/Bee/artifacts/1900b0aE.dag/(AssemblyZero\.[A-Za-z.]+?)(\.ref)?\.dll','Temp/P0P1/$1$2.dll'
  $listed=$r | Where-Object { $_ -match '\.cs"?$' } | ForEach-Object { $_.Trim('"').Replace('\','/') }
  $extra=Get-ChildItem $roots[$a] -Recurse -Filter *.cs | ForEach-Object { (Resolve-Path -Relative $_.FullName).Replace('\','/').Substring(2) } | Where-Object { $listed -notcontains $_ }
  $r=@($r)+@($extra | ForEach-Object { '"'+$_+'"' })
  # References added to an asmdef after Unity's last import are borrowed from the Editor response file.
  if($a -eq 'AssemblyZero.Unity'){
    $urp=Get-Content 'Library/Bee/artifacts/1900b0aE.dag/AssemblyZero.Unity.Editor.rsp' | Where-Object { $_ -match '^-r:.*RenderPipelines\.(Core|Universal)\.Runtime' -and $r -notcontains $_ }
    $r=@($r)+@($urp)
  }
  $r | Set-Content "$s/$a.rsp"
  $out=& "$EditorData/netcorerun/netcorerun.exe" "$EditorData/DotNetSdkRoslyn/csc.dll" "@$s/$a.rsp"
  $out | Select-String -Pattern 'error CS' | Select-Object -First 40 | ForEach-Object { $_.Line }
  if($LASTEXITCODE -ne 0){ "FAIL $a"; exit 1 } else { "PASS $a" }
}
