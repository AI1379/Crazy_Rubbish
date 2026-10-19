param(
    [string]$PlayerDirectory = 'Builds/WindowsRelease',
    [string]$ResultsDirectory = 'TestResults/merge-compatibility',
    [switch]$WithGraphics,
    [ValidateRange(640, 3840)][int]$Width = 1280,
    [ValidateRange(480, 2160)][int]$Height = 800,
    [ValidateRange(10, 600)][int]$TimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'The compatibility smoke test requires Windows.' }
$playerRoot = (Resolve-Path -LiteralPath $PlayerDirectory).Path
$playerPath = Join-Path $playerRoot 'CrazyRubbish.exe'
if (-not (Test-Path -LiteralPath $playerPath -PathType Leaf)) { throw 'Build the Windows player first.' }
$resultRoot = [System.IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $resultRoot -Force | Out-Null
$runKey = [Guid]::NewGuid().ToString('N')
$resultPath = Join-Path $resultRoot "compatibility-$runKey.json"
$logPath = Join-Path $resultRoot "compatibility-$runKey.log"
$arguments = @('-batchmode', '-mergeCompatibilitySmoke', '-mergeResults', ('"' + $resultPath + '"'),
    '-planSaveRoot', ('"' + (Join-Path $resultRoot ("unified-saves-$runKey")) + '"'),
    '-logFile', ('"' + $logPath + '"'), '-screen-width', $Width, '-screen-height', $Height, '-screen-fullscreen', '0')
if ($WithGraphics) { $arguments += @('-mergeScreenshot', ('"' + (Join-Path $resultRoot "unified-$Width-$Height.png") + '"')) }
else { $arguments += '-nographics' }
$process = Start-Process -FilePath $playerPath -ArgumentList $arguments -WorkingDirectory $playerRoot -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    Stop-Process -Id $process.Id -Force
    throw "Compatibility smoke timed out. See $logPath"
}
$process.Refresh()
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $resultPath)) { throw "Compatibility smoke failed. See $logPath" }
$report = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
foreach ($field in @('success', 'singleGame', 'inventoryUi', 'splitAndDrag', 'metadataAndSwap', 'capacityAndReservations', 'releaseRules', 'saveRoundtrip', 'interfaceSizes', 'constructionShopping')) {
    if ($report.$field -ne $true) { throw "Compatibility criterion failed: $field. See $resultPath" }
}
Write-Output ("Compatibility smoke passed: " + ($report | ConvertTo-Json -Compress))
Write-Output "Report: $resultPath"