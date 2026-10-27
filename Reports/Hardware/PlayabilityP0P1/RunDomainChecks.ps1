param([string[]]$Filter=@(),[switch]$WithRuntime,[string]$EditorData='D:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Data')
# Compiles Domain (and optionally Runtime) offline, then runs HardwareBusinessTests/EP04BusinessTests under Mono.
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path;Set-Location -LiteralPath $root
$assemblies=@('AssemblyZero.Domain');if($WithRuntime){$assemblies+='AssemblyZero.Unity'}
& (Join-Path $PSScriptRoot 'Compile.ps1') -Assemblies $assemblies -EditorData $EditorData;if($LASTEXITCODE -ne 0){exit 1}
$s='Temp/P0P1'
$nunit=(Get-ChildItem 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll' | Select-Object -First 1).FullName
$references=Get-Content 'Library/Bee/artifacts/1900b0aE.dag/AssemblyZero.Domain.rsp' | Where-Object { $_ -match '^-r:' }
$harness=@('-target:exe',"-out:$s/Harness.exe",'-langversion:9.0','-nologo','-nowarn:0618',"-r:$s/AssemblyZero.Domain.dll",('-r:"'+$nunit+'"'),'Reports/Hardware/PlayabilityP0P1/Harness.cs','Assets/_AssemblyZero/Tests/EditMode/HardwareBusinessTests.cs','Assets/_AssemblyZero/Tests/EditMode/EP04BusinessTests.cs')+$references
if($WithRuntime){$harness+="-r:$s/AssemblyZero.Unity.dll"}
$harness | Set-Content "$s/Harness.rsp"
& "$EditorData/netcorerun/netcorerun.exe" "$EditorData/DotNetSdkRoslyn/csc.dll" "@$s/Harness.rsp" | Select-String 'error CS' | ForEach-Object { $_.Line }
if($LASTEXITCODE -ne 0){ 'Harness compilation failed'; exit 1 }
Copy-Item -LiteralPath $nunit -Destination "$s/nunit.framework.dll" -Force
& (Join-Path $EditorData 'MonoBleedingEdge/bin/mono.exe') "$s/Harness.exe" @Filter
exit $LASTEXITCODE
