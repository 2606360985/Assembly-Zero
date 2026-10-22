param([string]$EditorData = 'D:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location -LiteralPath $workspace
$scratch = 'Temp/EP04Adjustments'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
$compileLog = 'Reports/Hardware/Adjustments-Compile.txt'
'Unity compiler + existing Bee response files; outputs isolated under Temp. No Unity Editor execution.' | Set-Content $compileLog
$compilerHost = Join-Path $EditorData 'netcorerun/netcorerun.exe'
$compiler = Join-Path $EditorData 'DotNetSdkRoslyn/csc.dll'
foreach ($assembly in @('AssemblyZero.Domain','AssemblyZero.Unity','AssemblyZero.Tests','AssemblyZero.PlayModeTests','AssemblyZero.Unity.Editor')) {
    $source = 'Library/Bee/artifacts/1900b0aE.dag/' + $assembly + '.rsp'
    $response = Get-Content -LiteralPath $source
    $response = $response -replace 'Library/Bee/artifacts/1900b0aE.dag/(AssemblyZero\.[A-Za-z.]+?)(\.ref)?\.dll', 'Temp/EP04Adjustments/$1$2.dll'
    $target = $scratch + '/' + $assembly + '.rsp'
    $response | Set-Content -LiteralPath $target
    & $compilerHost $compiler ('@' + $target) | Add-Content $compileLog
    if ($LASTEXITCODE -ne 0) { throw ('Compilation failed: ' + $assembly) }
    ('PASS compile ' + $assembly) | Add-Content $compileLog
}
$nunit = (Get-ChildItem 'Library/PackageCache/com.unity.ext.nunit*/net40/unity-custom/nunit.framework.dll' | Select-Object -First 1).FullName
$references = Get-Content 'Library/Bee/artifacts/1900b0aE.dag/AssemblyZero.Domain.rsp' | Where-Object { $_ -match '^-r:' }
$harness = @('-target:exe','-out:Temp/EP04Adjustments/AdjustmentsHarness.exe','-langversion:9.0','-nologo','-r:Temp/EP04Adjustments/AssemblyZero.Domain.dll','-r:Temp/EP04Adjustments/AssemblyZero.Unity.dll',('-r:"' + $nunit + '"'),'Reports/Hardware/AdjustmentsHarness.cs','Assets/_AssemblyZero/Tests/EditMode/HardwareBusinessTests.cs','Assets/_AssemblyZero/Tests/EditMode/EP04BusinessTests.cs') + $references
$harness | Set-Content ($scratch + '/Harness.rsp')
& $compilerHost $compiler ('@' + $scratch + '/Harness.rsp') | Add-Content $compileLog
if ($LASTEXITCODE -ne 0) { throw 'Harness compilation failed' }
Copy-Item -LiteralPath $nunit -Destination ($scratch + '/nunit.framework.dll')
& (Join-Path $EditorData 'MonoBleedingEdge/bin/mono.exe') ($scratch + '/AdjustmentsHarness.exe') | Tee-Object -FilePath 'Reports/Hardware/Adjustments-DomainChecks.txt'
if ($LASTEXITCODE -ne 0) { throw 'Fallback domain checks failed' }
