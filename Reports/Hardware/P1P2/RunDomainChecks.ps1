param([string[]]$Filter=@(),[string]$EditorData='D:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Data')
# Compiles Domain offline with the P0P1 compiler script, then runs every hardware domain test class under Mono.
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path;Set-Location -LiteralPath $root
& (Join-Path $root 'Reports/Hardware/PlayabilityP0P1/Compile.ps1') -Assemblies @('AssemblyZero.Domain') -EditorData $EditorData;if($LASTEXITCODE -ne 0){exit 1}
$s='Temp/P0P1'
$nunit=(Get-ChildItem 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll' | Select-Object -First 1).FullName
$references=Get-Content 'Library/Bee/artifacts/1900b0aE.dag/AssemblyZero.Domain.rsp' | Where-Object { $_ -match '^-r:' }
$tests=@('HardwareBusinessTests','HardwareProcurementTests','HardwareResearchTests','HardwareSkuTests','HardwareIterationTests','EP04BusinessTests') | ForEach-Object { "Assets/_AssemblyZero/Tests/EditMode/$_.cs" } | Where-Object { Test-Path $_ }
$harness=@('-target:exe',"-out:$s/P1P2Harness.exe",'-langversion:9.0','-nologo','-nowarn:0618',"-r:$s/AssemblyZero.Domain.dll",('-r:"'+$nunit+'"'),'Reports/Hardware/P1P2/Harness.cs')+$tests+$references
$harness | Set-Content "$s/P1P2Harness.rsp"
& "$EditorData/netcorerun/netcorerun.exe" "$EditorData/DotNetSdkRoslyn/csc.dll" "@$s/P1P2Harness.rsp" | Select-String 'error CS' | ForEach-Object { $_.Line }
if($LASTEXITCODE -ne 0){ 'Harness compilation failed'; exit 1 }
Copy-Item -LiteralPath $nunit -Destination "$s/nunit.framework.dll" -Force
& (Join-Path $EditorData 'MonoBleedingEdge/bin/mono.exe') "$s/P1P2Harness.exe" @Filter
exit $LASTEXITCODE
