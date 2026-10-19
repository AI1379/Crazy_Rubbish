param(
    [string]$PlayerDirectory = 'Builds/WindowsRelease',
    [string]$ResultsDirectory = 'TestResults/ci-smoke',
    [string]$ExpectedVersion = '',
    [switch]$Legacy,
    [switch]$FullGame,
    [switch]$WithGraphics,
    [ValidateRange(640, 3840)][int]$Width = 1280,
    [ValidateRange(480, 2160)][int]$Height = 800,
    [switch]$Automation,
    [ValidateRange(10, 600)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'The Windows player smoke test must run on Windows.' }
if ($Legacy) { throw 'The player now ships one unified game. Run Unity EditMode tests for the historical Prototype rules.' }
# Automation and FullGame remain accepted aliases: the unified acceptance always includes those checks.
$playerRoot = (Resolve-Path -LiteralPath $PlayerDirectory).Path
$playerPath = Join-Path $playerRoot 'CrazyRubbish.exe'
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) { throw "Player executable missing: $playerPath" }
$resultRoot = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
# Unique paths prevent a prior successful run from satisfying this invocation.
$runKey = [Guid]::NewGuid().ToString('N')
$resultPath = Join-Path $resultRoot "smoke-$runKey.json"
$logPath = Join-Path $resultRoot "smoke-$runKey.log"
$arguments = @('-batchmode', '-planSmoke', '-planResults', ('"' + $resultPath + '"'),
    '-logFile', ('"' + $logPath + '"'), '-planSaveRoot', ('"' + (Join-Path $resultRoot ("saves-$runKey")) + '"'))
$arguments += @('-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0')
if ($WithGraphics) {
    $screenshotFlag = '-planScreenshot'
    $arguments += @($screenshotFlag, ('"' + (Join-Path $resultRoot ("preview-$Width-$Height-$runKey.png")) + '"'))
} else { $arguments += '-nographics' }
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -WorkingDirectory $playerRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "Windows smoke test timed out after $TimeoutSeconds seconds. See $logPath"
}
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Windows player exited $($process.ExitCode). See $logPath" }
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw "Smoke report missing. See $logPath" }
$report = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
if (-not $report.success -or $report.activeGame -cne 'S6Plan' -or -not $report.releaseReadiness) {
    throw "Smoke acceptance criteria failed. See $resultPath"
}
if ($report.development -ne $false) { throw 'Release player was built with development options, or omitted build metadata.' }
foreach($required in @('realZeroCashStarter','diskSaveLoad','stability','automationUi','beltDrag','region3','returnJourney','tutorialHud','selectionAnimation','productionCatalog','buildingSearch','riskAlerts','settingsUi')){
    if($report.$required -ne $true){throw "Current game acceptance missing or failed: $required. See $resultPath"}
}
if ($ExpectedVersion -and $report.version -cne $ExpectedVersion) {
    throw "Expected player version $ExpectedVersion, got $($report.version)."
}
Write-Output ("Windows smoke passed: " + ($report | ConvertTo-Json -Compress))
