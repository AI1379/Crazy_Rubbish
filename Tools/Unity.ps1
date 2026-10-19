param(
    [ValidateSet('Import', 'Open', 'Setup', 'ValidateArt', 'ValidatePlan', 'Test', 'Build', 'Release', 'Smoke', 'SmokePlan')]
    [string]$Action = 'Import',
    [string]$UnityPath,
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$ReleaseVersion
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
if (-not $versionLine) { throw 'Cannot read the required Unity Editor version.' }
$editorVersion = ($versionLine -split ': ', 2)[1].Trim()

if (-not $UnityPath) {
    $UnityPath = Join-Path $env:ProgramFiles "Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $editorVersion was not found. Supply -UnityPath with the Editor executable path."
}
$editorPath = (Resolve-Path -LiteralPath $UnityPath).Path

# Windows Start-Process joins ArgumentList into a command line; quote path arguments explicitly.
$quotedProject = '"' + $projectRoot + '"'
if ($Action -eq 'Open') {
    Start-Process -FilePath $editorPath -ArgumentList @('-projectPath', $quotedProject) -WindowStyle Hidden | Out-Null
    Write-Output "Opened project with Unity $editorVersion."
    exit 0
}

# Let Unity check its project lock; a failed import can leave an unlocked lockfile behind.
$logDirectory = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logFile = Join-Path $logDirectory ($Action.ToLowerInvariant() + '.log')
$quotedLog = '"' + $logFile + '"'
$editorArguments = @('-batchmode', '-projectPath', $quotedProject, '-logFile', $quotedLog)
$runPath = $editorPath
$resultFile = $null
switch ($Action) {
    'Import' { $editorArguments += @('-nographics', '-quit') }
    'Setup' { $editorArguments += @('-nographics', '-quit', '-executeMethod', 'CrazyRubbish.Editor.PrototypeProjectSetup.Setup') }
    'ValidateArt' { $editorArguments += @('-nographics', '-quit', '-executeMethod', 'CrazyRubbish.Editor.LocalArtInstaller.Validate') }
    'ValidatePlan' { $editorArguments += @('-nographics', '-quit', '-executeMethod', 'CrazyRubbish.Editor.PlanProjectSetup.Validate') }
    'Test' {
        $resultDirectory = Join-Path $projectRoot 'TestResults'
        New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
        $resultFile = Join-Path $resultDirectory 'editmode.xml'
        $editorArguments += @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + $resultFile + '"'))
    }
    'Build' {
        $editorArguments += @('-nographics', '-quit', '-buildTarget', 'StandaloneWindows64', '-executeMethod', 'CrazyRubbish.Editor.PrototypeProjectSetup.BuildWindows')
    }
    'Release' {
        $editorArguments += @('-nographics', '-quit', '-buildTarget', 'StandaloneWindows64', '-executeMethod', 'CrazyRubbish.Editor.PrototypeProjectSetup.BuildWindowsRelease')
        if ($ReleaseVersion) { $editorArguments += @('-releaseVersion', $ReleaseVersion) }
    }
    { $_ -in 'Smoke','SmokePlan' } {
        $runPath = Join-Path $projectRoot 'Builds\Windows\CrazyRubbish.exe'
        if (-not (Test-Path -LiteralPath $runPath -PathType Leaf)) { throw 'Run Build before Smoke.' }
        $resultDirectory = Join-Path $projectRoot 'TestResults'
        $artifactDirectory = Join-Path $projectRoot 'Artifacts'
        New-Item -ItemType Directory -Path $resultDirectory, $artifactDirectory -Force | Out-Null
        $resultFile = Join-Path $resultDirectory $(if ($Action -eq 'SmokePlan') {'plan-smoke.json'} else {'standalone-smoke.json'})
        $screenshotFile = Join-Path $artifactDirectory $(if ($Action -eq 'SmokePlan') {'Plan-preview.png'} else {'M2-preview.png'})
        $smokeFlag = if ($Action -eq 'SmokePlan') {'-planSmoke'} else {'-prototypeSmoke'}
        $resultsFlag = if ($Action -eq 'SmokePlan') {'-planResults'} else {'-prototypeResults'}
        $screenshotFlag = if ($Action -eq 'SmokePlan') {'-planScreenshot'} else {'-prototypeScreenshot'}
        $editorArguments = @('-batchmode', '-screen-width', '1280', '-screen-height', '800', '-screen-fullscreen', '0',
            '-logFile', $quotedLog, $smokeFlag, $resultsFlag, ('"' + $resultFile + '"'),
            $screenshotFlag, ('"' + $screenshotFile + '"'))
        if ($Action -eq 'SmokePlan') { $editorArguments += @('-planSaveRoot', ('"' + (Join-Path $resultDirectory 'PlanSmokeSaves') + '"')) }
    }
}
$startedAt = [DateTime]::UtcNow
$editorProcess = Start-Process -FilePath $runPath -ArgumentList $editorArguments -WindowStyle Hidden -PassThru
# Unity can leave shared licensing/package helpers running; wait for the Editor/player itself.
$editorProcess.WaitForExit()
$editorProcess.Refresh()

if ($editorProcess.ExitCode -ne 0) {
    throw "Unity $Action failed with exit code $($editorProcess.ExitCode). See $logFile"
}
if ($resultFile) {
    if (-not (Test-Path -LiteralPath $resultFile) -or (Get-Item -LiteralPath $resultFile).LastWriteTimeUtc -lt $startedAt) {
        throw "No fresh $Action result was produced. See $logFile"
    }
    if ($Action -eq 'Test') {
        [xml]$testReport = Get-Content -Raw -LiteralPath $resultFile
        if ($testReport.'test-run'.result -ne 'Passed') { throw "Tests did not pass. See $resultFile" }
        Write-Output ("Tests passed: " + $testReport.'test-run'.passed)
    } elseif ($Action -in 'Smoke','SmokePlan') {
        $smokeReport = Get-Content -Raw -LiteralPath $resultFile | ConvertFrom-Json
        if (-not $smokeReport.success) { throw "Standalone smoke test failed. See $resultFile" }
        Write-Output ("Standalone smoke passed: " + ($smokeReport | ConvertTo-Json -Compress))
    }
}
Write-Output "Unity $Action succeeded. Log: $logFile"
