param(
    [string]$ReleaseTag = '',
    [string]$ManualVersion = '',
    [ValidateRange(1, 2147483647)][int]$RunNumber = 1
)

$ErrorActionPreference = 'Stop'
$version = if ($ReleaseTag) { $ReleaseTag } elseif ($ManualVersion) { $ManualVersion } else { "0.0.$RunNumber-ci" }
if ($version.Length -gt 80 -or $version -cnotmatch '^v?[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?\z') {
    throw 'Use a version/tag such as v0.2.0 or v0.2.0-rc.1; paths, whitespace and shell characters are not accepted.'
}
$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$versionPath = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'
$unityLine = Get-Content -LiteralPath $versionPath | Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
if (-not $unityLine) { throw 'Unity Editor version missing.' }
$unityVersion = ($unityLine -split ': ', 2)[1].Trim()
$metadata = [ordered]@{
    version = $version; build_version = $version.TrimStart('v'); unity_version = $unityVersion;
    artifact = "windows-$version"; zip = "CrazyRubbish-$version-Windows-x64.zip"
}
if ($env:GITHUB_OUTPUT) {
    foreach ($entry in $metadata.GetEnumerator()) {
        "$($entry.Key)=$($entry.Value)" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Append -Encoding utf8
    }
}
[pscustomobject]$metadata
