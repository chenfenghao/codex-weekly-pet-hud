param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = 'win-x64',
    [switch]$AutoStart
)

$Script = Join-Path $PSScriptRoot "platforms\windows\scripts\install.ps1"
& $Script -Runtime $Runtime -AutoStart:$AutoStart
exit $LASTEXITCODE
