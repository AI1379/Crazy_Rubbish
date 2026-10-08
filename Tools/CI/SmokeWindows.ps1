param(
    [string]$PlayerDirectory = 'Builds/WindowsRelease',
    [string]$ResultsDirectory = 'TestResults/ci-smoke',
    [string]$ExpectedVersion = '',
    [ValidateRange(10, 600)][int]$TimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'The Windows player smoke test must run on Windows.' }
$playerRoot = (Resolve-Path -LiteralPath $PlayerDirectory).Path
$playerPath = Join-Path $playerRoot 'CrazyRubbish.exe'
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) { throw "Player executable missing: $playerPath" }
$resultRoot = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
# Unique paths prevent a prior successful run from satisfying this invocation.
$runKey = [Guid]::NewGuid().ToString('N')
$resultPath = Join-Path $resultRoot "smoke-$runKey.json"
$logPath = Join-Path $resultRoot "smoke-$runKey.log"
$arguments = @('-batchmode', '-nographics', '-prototypeSmoke', '-prototypeResults', ('"' + $resultPath + '"'),
    '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -WorkingDirectory $playerRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "Windows smoke test timed out after $TimeoutSeconds seconds. See $logPath"
}
$process.Refresh()
if ($process.ExitCode -ne 0) { throw "Windows player exited $($process.ExitCode). See $logPath" }
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw "Smoke report missing. See $logPath" }
$report = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
if (-not $report.success -or -not $report.restart -or $report.residents -ne 5 -or $report.wagesPaidCents -ne 5000 -or $report.batches -lt 12) {
    throw "Smoke acceptance criteria failed. See $resultPath"
}
if ($report.development -ne $false) { throw 'Release player was built with development options, or omitted build metadata.' }
if ($ExpectedVersion -and $report.version -cne $ExpectedVersion) {
    throw "Expected player version $ExpectedVersion, got $($report.version)."
}
Write-Output ("Windows smoke passed: " + ($report | ConvertTo-Json -Compress))
