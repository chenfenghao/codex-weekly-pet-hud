param(
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64',
    [switch]$AutoStart
)
$ErrorActionPreference = 'Stop'
$windowsRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $windowsRoot 'CodexPetLimitRings.Windows/CodexPetLimitRings.Windows.csproj'
$artifact = Join-Path $windowsRoot ('artifacts/install-' + [guid]::NewGuid().ToString('N'))
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs/CodexWeeklyPetHud'
$executable = Join-Path $installRoot 'CodexWeeklyPetHud.exe'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 8 SDK before installing from source.' }
dotnet publish $project -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -o $artifact
if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
$running = @(Get-Process CodexWeeklyPetHud -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable })
foreach ($process in $running) {
    Stop-Process -Id $process.Id
    if (-not $process.WaitForExit(10000)) { throw 'Close the running HUD and retry.' }
}
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Get-ChildItem -LiteralPath $artifact -File | Where-Object { $_.Extension -in '.exe','.dll' } |
    ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $installRoot -Force }
if ($AutoStart) {
    New-Item -Path $runKey -Force | Out-Null
    New-ItemProperty -Path $runKey -Name 'CodexWeeklyPetHud' -Value ('"' + $executable + '"') -PropertyType String -Force | Out-Null
}
Start-Process -FilePath $executable -WorkingDirectory $installRoot -WindowStyle Hidden
Write-Output "Installed: $executable"
