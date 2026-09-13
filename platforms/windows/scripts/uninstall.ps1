param([switch]$Data)
$ErrorActionPreference = 'Stop'
$localRoot = [IO.Path]::GetFullPath($env:LOCALAPPDATA)
$installRoot = [IO.Path]::GetFullPath((Join-Path $localRoot 'Programs/CodexWeeklyPetHud'))
$dataRoot = [IO.Path]::GetFullPath((Join-Path $localRoot 'CodexWeeklyPetHud'))
$executable = Join-Path $installRoot 'CodexWeeklyPetHud.exe'
foreach ($target in @($installRoot,$dataRoot)) {
    if (-not $target.StartsWith($localRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected uninstall target.' }
    if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Refusing to remove a redirected application directory.' }
}
$running = @(Get-Process CodexWeeklyPetHud -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $executable })
foreach ($process in $running) {
    Stop-Process -Id $process.Id
    if (-not $process.WaitForExit(10000)) { throw 'Close the running HUD and retry.' }
}
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CodexWeeklyPetHud' -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $installRoot) { Remove-Item -LiteralPath $installRoot -Recurse -Force }
if ($Data -and (Test-Path -LiteralPath $dataRoot)) { Remove-Item -LiteralPath $dataRoot -Recurse -Force }
Write-Output 'Uninstalled Codex Weekly Pet HUD. Portable copies, if any, must be removed separately.'
