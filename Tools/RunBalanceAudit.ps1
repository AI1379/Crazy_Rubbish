param(
    [Parameter(Mandatory=$true)][string]$UnityPath,
    [int[]]$Seeds=@(4271,1379,42),
    [ValidateSet('baseline-final','semi-final','managed-four-final','both','all')][string]$Scenario='all',
    [string]$OutputRoot
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $OutputRoot){$OutputRoot=Join-Path $projectRoot 'Artifacts/BalanceAudit'}
$core=Join-Path $projectRoot 'Library/ScriptAssemblies/CrazyRubbish.Core.dll'
if(-not (Test-Path -LiteralPath $core)){throw 'Import the Unity project first so CrazyRubbish.Core.dll exists.'}
$editorRoot=Split-Path (Resolve-Path -LiteralPath $UnityPath).Path -Parent
$mono=Join-Path $editorRoot 'Data/MonoBleedingEdge/bin/mono.exe'
$facade=Join-Path $editorRoot 'Data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
foreach($required in @($mono,$facade,$compiler)){if(-not (Test-Path -LiteralPath $required)){throw "Required runtime missing: $required"}}
New-Item -ItemType Directory -Path $OutputRoot,(Join-Path $OutputRoot 'lib') -Force | Out-Null
$library=Join-Path $OutputRoot 'lib/CrazyRubbish.Core.dll'
Copy-Item -LiteralPath $core -Destination $library -Force
$executable=Join-Path $OutputRoot 'LongPlay.exe'
$source=Join-Path $OutputRoot 'LongPlay.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Balance/LongPlay.cs') -Destination $source -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets/_Game/Resources/Plan/RuntimeConfig.json') -Destination (Join-Path $OutputRoot 'RuntimeConfig.json') -Force
$compileArgs=@('/nologo',('/out:'+$executable),('/r:'+$library),('/r:'+$facade),'/r:System.Web.Extensions.dll',$source)
& $compiler @compileArgs
if($LASTEXITCODE -ne 0){throw 'Balance driver compilation failed.'}
$env:MONO_PATH=Split-Path $library -Parent
$scenarios=if($Scenario -eq 'all'){@('baseline-final','semi-final','managed-four-final')}elseif($Scenario -eq 'both'){@('baseline-final','managed-four-final')}else{@($Scenario)}
$results=@()
foreach($mode in $scenarios){foreach($auditSeed in $Seeds){
    $data=Join-Path $OutputRoot $mode
    $log=Join-Path $OutputRoot ($mode+'-'+$auditSeed+'.log')
    & $mono $executable (Join-Path $OutputRoot 'RuntimeConfig.json') $data $auditSeed $mode *> $log
    if($LASTEXITCODE -ne 0){throw "Balance driver failed. See $log"}
    $reportPath=Join-Path $data ('report-'+$auditSeed+'.json')
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if($report.cashLedgerDifference -ne 0){throw "Cash ledger mismatch. See $reportPath"}
    $results+=$report
    $report | Select-Object mode,seed,elapsed,region,firstAffordableRegion2,finalCash,ending,cashLedgerDifference | ConvertTo-Json -Compress | Write-Output
}}
$results | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $OutputRoot 'suite.json') -Encoding utf8
$blocked=@($results | Where-Object { $_.ending -or @($_.events | Where-Object { $_.name -like 'BLOCKED:*' }).Count -gt 0 })
if($blocked.Count -gt 0){throw 'One or more audit policies were blocked or reached an ending. Reports have been preserved; inspect suite.json.'}
