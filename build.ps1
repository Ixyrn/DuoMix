param([string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Source/DuoMix.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project
$version = [string]$projectXml.Project.PropertyGroup.Version
$dist = Join-Path $PSScriptRoot 'dist'
$release = Join-Path $dist "DuoMix-$version-win-x64"
if (Test-Path -LiteralPath $release) { throw "Output already exists: $release. Move it aside before rebuilding." }
New-Item -ItemType Directory -Path $release -Force | Out-Null
& $DotNet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $release
if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
foreach ($name in @('LICENSE', 'README.md', 'THIRD-PARTY.md')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $release
}
foreach ($name in @('Notices', 'VirtualCable')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $release -Recurse
}
if (Get-ChildItem -LiteralPath $release -Filter '*.pdb' -Recurse) { throw 'Debug symbols must not be shipped.' }
$archive = Join-Path $dist "DuoMix-$version-win-x64.zip"
Compress-Archive -LiteralPath $release -DestinationPath $archive -CompressionLevel Optimal
$digest = Get-FileHash -LiteralPath $archive -Algorithm SHA256
'{0}  {1}' -f $digest.Hash, [IO.Path]::GetFileName($archive) | Set-Content (Join-Path $dist 'SHA256SUMS.txt')
Write-Host "Ready: $archive"
