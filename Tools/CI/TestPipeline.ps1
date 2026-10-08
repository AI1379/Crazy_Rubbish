$ErrorActionPreference = 'Stop'
$script:ciAssertions = 0
function Assert-Ci([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:ciAssertions++
}
function Assert-CiThrows([scriptblock]$Action, [string]$Message) {
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    Assert-Ci $threw $Message
}
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixtureBase = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'Artifacts/CI-tests'))
$fixtureRoot = Join-Path $fixtureBase ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
try {
    foreach ($scriptFile in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Tools') -Recurse -Filter '*.ps1') {
        $parseTokens = $null; $parseErrors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($scriptFile.FullName, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
        Assert-Ci ($parseErrors.Count -eq 0) "PowerShell syntax errors in $($scriptFile.Name): $parseErrors"
    }
    $metadata = & (Join-Path $PSScriptRoot 'GetBuildMetadata.ps1') -ReleaseTag 'v1.2.3-rc.1' -RunNumber 9
    Assert-Ci ($metadata.version -ceq 'v1.2.3-rc.1') 'Release tag must be preserved in package names.'
    Assert-Ci ($metadata.build_version -ceq '1.2.3-rc.1') 'Unity version should omit the tag v prefix.'
    $manual = & (Join-Path $PSScriptRoot 'GetBuildMetadata.ps1') -ManualVersion '0.3.0' -RunNumber 9
    Assert-Ci ($manual.version -ceq '0.3.0') 'Manual version was ignored.'
    $snapshot = & (Join-Path $PSScriptRoot 'GetBuildMetadata.ps1') -RunNumber 9
    Assert-Ci ($snapshot.version -ceq '0.0.9-ci') 'Snapshot version must distinguish CI runs.'
    foreach ($badTag in @('../v1.0.0', 'v1.0.0;echo', 'v1.0.0/other', "v1.0.0`n")) {
        Assert-CiThrows { & (Join-Path $PSScriptRoot 'GetBuildMetadata.ps1') -ReleaseTag $badTag } "Unsafe tag accepted: $badTag"
    }
    $build = Join-Path $fixtureRoot 'player with spaces'
    New-Item -ItemType Directory -Path (Join-Path $build 'CrazyRubbish_Data/Managed'), (Join-Path $build 'MonoBleedingEdge/EmbedRuntime') -Force | Out-Null
    foreach ($file in @('CrazyRubbish.exe', 'UnityPlayer.dll', 'Font-OFL.txt', 'CrazyRubbish_Data/Managed/CrazyRubbish.Core.dll', 'MonoBleedingEdge/EmbedRuntime/mono.dll')) {
        [System.IO.File]::WriteAllText((Join-Path $build $file), "fixture-$file")
    }
    $output = Join-Path $fixtureRoot 'packages with spaces'
    $revision = 'a' * 40
    & (Join-Path $PSScriptRoot 'PackageWindows.ps1') -BuildDirectory $build -OutputDirectory $output -Version 'v1.2.3-rc.1' -Commit $revision -ReleaseTag 'v1.2.3-rc.1' | Out-Null
    $zipPath = Join-Path $output 'CrazyRubbish-v1.2.3-rc.1-Windows-x64.zip'
    Assert-Ci (Test-Path -LiteralPath $zipPath -PathType Leaf) 'Versioned ZIP missing.'
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksum = [System.IO.File]::ReadAllText($zipPath + '.sha256')
    Assert-Ci ($checksum -ceq "$hash  CrazyRubbish-v1.2.3-rc.1-Windows-x64.zip`n") 'Checksum does not match the archive.'
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        Assert-Ci ($null -ne $zip.GetEntry('CrazyRubbish.exe')) 'Executable must be at ZIP root.'
        Assert-Ci ($null -ne $zip.GetEntry('MonoBleedingEdge/EmbedRuntime/mono.dll')) 'Mono runtime was not packaged.'
        Assert-Ci ($null -ne $zip.GetEntry('CrazyRubbish_Data/Managed/CrazyRubbish.Core.dll')) 'Managed player data was not packaged.'
        $reader = [System.IO.StreamReader]::new($zip.GetEntry('build-info.json').Open())
        try { $info = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        Assert-Ci ($info.commit -ceq $revision -and $info.version -ceq '1.2.3-rc.1' -and $info.releaseTag -ceq 'v1.2.3-rc.1') 'Build provenance is incorrect.'
    } finally { $zip.Dispose() }
    Assert-CiThrows { & (Join-Path $PSScriptRoot 'PackageWindows.ps1') -BuildDirectory $build -OutputDirectory $output -Version 'v1.2.3-rc.1' -Commit $revision } 'Existing ZIP was silently overwritten.'
    Assert-CiThrows { & (Join-Path $PSScriptRoot 'PackageWindows.ps1') -BuildDirectory $build -OutputDirectory (Join-Path $build 'nested') -Version '1.2.3' -Commit $revision } 'Output inside the source player must be rejected.'
    Remove-Item -LiteralPath (Join-Path $build 'UnityPlayer.dll')
    Assert-CiThrows { & (Join-Path $PSScriptRoot 'PackageWindows.ps1') -BuildDirectory $build -OutputDirectory (Join-Path $fixtureRoot 'missing') -Version '1.2.4' -Commit $revision } 'Incomplete runtime was accepted.'
    Write-Output "Pipeline checks passed: $script:ciAssertions"
} finally {
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolvedFixture.StartsWith($fixtureBase + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Fixture cleanup target escaped the CI test directory.'
    }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
