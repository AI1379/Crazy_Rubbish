param([string]$Destination = 'Artifacts/CI/actionlint-1.7.12')

$ErrorActionPreference = 'Stop'
$version = '1.7.12'
$root = [System.IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $root -Force | Out-Null
$platform = if ($IsWindows) { 'windows_amd64.zip' } elseif ($IsLinux) { 'linux_amd64.tar.gz' } else { throw 'actionlint installer supports Windows/Linux x64.' }
$archiveName = "actionlint_${version}_$platform"
$baseUrl = "https://github.com/rhysd/actionlint/releases/download/v$version"
$archivePath = Join-Path $root $archiveName
$checksumsPath = Join-Path $root 'checksums.txt'
Invoke-WebRequest -Uri "$baseUrl/$archiveName" -OutFile $archivePath -MaximumRetryCount 3 -RetryIntervalSec 1
Invoke-WebRequest -Uri "$baseUrl/actionlint_${version}_checksums.txt" -OutFile $checksumsPath -MaximumRetryCount 3 -RetryIntervalSec 1
$checksumLine = Get-Content -LiteralPath $checksumsPath | Where-Object { ($_ -split '\s+')[-1] -eq $archiveName }
if (-not $checksumLine -or (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne ($checksumLine -split '\s+')[0]) {
    throw 'actionlint download checksum mismatch.'
}
if ($IsWindows) { Expand-Archive -LiteralPath $archivePath -DestinationPath $root -Force }
else {
    & tar -xzf $archivePath -C $root
    if ($LASTEXITCODE -ne 0) { throw 'Failed to extract actionlint.' }
}
$executable = Join-Path $root $(if ($IsWindows) { 'actionlint.exe' } else { 'actionlint' })
if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Extracted actionlint executable missing.' }
Write-Output $executable
