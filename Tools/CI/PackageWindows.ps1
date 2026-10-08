param(
    [string]$BuildDirectory = 'Builds/WindowsRelease',
    [string]$OutputDirectory = 'Artifacts/Release',
    [Parameter(Mandatory)][ValidatePattern('^v?[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('^[a-fA-F0-9]{40}$')][string]$Commit,
    [string]$Repository = 'AI1379/Crazy_Rubbish',
    [string]$ReleaseTag = '',
    [string]$UnityVersion = '2022.3.62f3'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$buildRoot = (Resolve-Path -LiteralPath $BuildDirectory).Path
foreach ($name in @('CrazyRubbish.exe', 'UnityPlayer.dll', 'Font-OFL.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $buildRoot $name) -PathType Leaf)) { throw "Build missing required file: $name" }
}
foreach ($name in @('CrazyRubbish_Data', 'MonoBleedingEdge')) {
    $directory = Join-Path $buildRoot $name
    if (-not (Test-Path -LiteralPath $directory -PathType Container) -or -not (Get-ChildItem -LiteralPath $directory -Recurse -File | Select-Object -First 1)) {
        throw "Build missing nonempty runtime directory: $name"
    }
}
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
if ($outputRoot.StartsWith($buildRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $outputRoot -eq $buildRoot) {
    throw 'Package output must be outside the player directory.'
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$packageName = "CrazyRubbish-$Version-Windows-x64.zip"
$packagePath = Join-Path $outputRoot $packageName
if (Test-Path -LiteralPath $packagePath) { throw "Package already exists; choose a clean output directory: $packagePath" }
$metadata = [ordered]@{
    product = 'CrazyRubbish'; version = $Version.TrimStart('v'); releaseTag = $ReleaseTag;
    commit = $Commit.ToLowerInvariant(); repository = $Repository; unityVersion = $UnityVersion;
    target = 'StandaloneWindows64'; scriptingBackend = 'Mono'; development = $false;
    builtAtUtc = [DateTime]::UtcNow.ToString('o')
}
$metadataPath = Join-Path $buildRoot 'build-info.json'
[System.IO.File]::WriteAllText($metadataPath, ($metadata | ConvertTo-Json), [System.Text.UTF8Encoding]::new($false))
[System.IO.Compression.ZipFile]::CreateFromDirectory($buildRoot, $packagePath, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$zip = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    foreach ($required in @('CrazyRubbish.exe','UnityPlayer.dll','Font-OFL.txt','build-info.json')) {
        if ($required -notin $entries) { throw "Archive missing required file: $required" }
    }
    if (-not ($entries | Where-Object { $_.StartsWith('CrazyRubbish_Data/') }) -or
        -not ($entries | Where-Object { $_.StartsWith('MonoBleedingEdge/') })) { throw 'Archive missing player data or Mono runtime.' }
} finally { $zip.Dispose() }
$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText($packagePath + '.sha256', "$hash  $packageName`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Packaged $packagePath"
if ($env:GITHUB_OUTPUT) {
    "zip=$packageName" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "checksum=$packageName.sha256" | Out-File -LiteralPath $env:GITHUB_OUTPUT -Append -Encoding utf8
}
