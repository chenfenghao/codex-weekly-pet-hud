[CmdletBinding()]
param(
    [ValidatePattern('^v\d+\.\d+\.\d+$')][string]$Tag = 'v1.2.0',
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$project = Join-Path $repoRoot 'platforms/windows/CodexPetLimitRings.Windows/CodexPetLimitRings.Windows.csproj'
$buildRoot = Join-Path $repoRoot ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
$publishRoot = Join-Path $buildRoot 'publish'
$packageName = "Codex-Weekly-Pet-HUD-$Tag-Windows-x64"
$packageRoot = Join-Path $buildRoot $packageName
$dist = Join-Path $repoRoot 'dist'
$version = $Tag.Substring(1)
& $DotnetPath publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true "-p:Version=$version" -p:DebugType=none -p:DebugSymbols=false -o $publishRoot --nologo
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
New-Item -ItemType Directory -Path $packageRoot,$dist -Force | Out-Null
# A fresh staging directory and explicit extensions prevent cached settings or logs entering releases.
Get-ChildItem -LiteralPath $publishRoot -File | Where-Object { $_.Extension -in '.exe','.dll' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $packageRoot }
foreach ($document in 'README.md','README.en.md','LICENSE','CHANGELOG.md') {
    Copy-Item -LiteralPath (Join-Path $repoRoot $document) -Destination $packageRoot
}
$imageRoot = Join-Path $packageRoot 'docs/images'
$platformDocs = Join-Path $packageRoot 'platforms/windows'
New-Item -ItemType Directory -Path $imageRoot,$platformDocs -Force | Out-Null
foreach ($name in 'capsule.png','radar-alert.png','capsule.en.png','radar-alert.en.png','taskbar.zh-CN.png','taskbar.en.png') {
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs/images/$name") -Destination $imageRoot
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'platforms/windows/README.md') -Destination $platformDocs
Copy-Item -LiteralPath (Join-Path $repoRoot 'platforms/windows/README.en.md') -Destination $platformDocs
# Include the licenses of the exact NuGet runtimes selected by restore.
$assets = Get-Content -LiteralPath (Join-Path (Split-Path $project) 'obj/project.assets.json') -Raw | ConvertFrom-Json
$notices = Join-Path $packageRoot 'licenses'
New-Item -ItemType Directory -Path $notices -Force | Out-Null
foreach ($runtimeName in 'Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64') {
    $runtime = $assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object { $_.name -eq $runtimeName } | Select-Object -First 1
    if (-not $runtime) { throw "Missing restored runtime: $runtimeName" }
    $runtimeVersion = $runtime.version.Trim('[',']').Split(',')[0].Trim()
    $runtimePath = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder ($runtimeName.ToLowerInvariant() + '/' + $runtimeVersion)
        if (Test-Path -LiteralPath $candidate) { $runtimePath = $candidate; break }
    }
    if (-not $runtimePath) { throw "Missing runtime license folder: $runtimeName" }
    $licenseFiles = @(Get-ChildItem -LiteralPath $runtimePath -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' })
    if (-not $licenseFiles.Count) { throw "Missing runtime license: $runtimeName" }
    foreach ($license in $licenseFiles) {
        Copy-Item -LiteralPath $license.FullName -Destination (Join-Path $notices ($runtimeName + '-' + $license.Name))
    }
}
$archive = Join-Path $dist ($packageName + '.zip')
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archive -CompressionLevel Optimal -Force
$checksum = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Value "$checksum  $packageName.zip" -Encoding ascii
Write-Output "Package: $archive"
Write-Output "SHA256: $checksum"
