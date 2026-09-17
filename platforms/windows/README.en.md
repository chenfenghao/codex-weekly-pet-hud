# Windows development and usage

[简体中文](README.md) · **English**

This fork is a .NET 8 / WPF tray application with a weekly quota HUD above the real Codex Pet. See the [main README](../../README.en.md) for usage instructions.

v1.2.0 also supports an independent taskbar mode. `displayMode` is `pet` (backward-compatible default) or `taskbar`; `taskbarOffset` is a separate leftward DIP offset. `TaskbarHudHost` owns a child HWND inside the primary horizontal taskbar, matching its DPI context without injecting into Explorer or resizing the app-button area. Unsupported layouts retain the tray entry. Windows 10 native placement was tested; Windows 11, Explorer restart and auto-hide need additional validation on target desktops.

Run `CodexWeeklyPetHud.exe --taskbar-self-test <output-directory>` on an interactive desktop to check real taskbar parenting, non-topmost style, native hit tests, notification-area bounds, hide/recreate, bilingual rendering and settings switching. The test uses sample data without reading account quota.

## Building and source installation

Requires the .NET 8 SDK, Windows x64 and PowerShell. Run these commands from the repository root:

```powershell
dotnet build platforms/windows/CodexPetLimitRings.Windows/CodexPetLimitRings.Windows.csproj -c Release
pwsh -File scripts/release/package-weekly.ps1 -Tag v1.2.0
```

Use `-DotnetPath C:\path\to\dotnet.exe` to select a custom SDK. Packages are written to `dist` and include Chinese and English documentation.

To install from source into `%LOCALAPPDATA%\Programs\CodexWeeklyPetHud`:

```powershell
.\install.ps1
# Register startup only when explicitly requested:
.\install.ps1 -AutoStart
.\uninstall.ps1
# Optionally delete this app's data directory too:
.\uninstall.ps1 -Data
```

Source installation requires the SDK; the downloaded portable build does not. Exit any copy running from another directory before installation.

## Checks

First publish to `artifacts/windows-x64` using the command in the main README:

```powershell
dotnet run --project platforms/windows/tests/LayoutTests/LayoutTests.csproj -c Release
$exe = (Resolve-Path artifacts/windows-x64/CodexWeeklyPetHud.exe).Path
Start-Process $exe -ArgumentList '--pace-self-test artifacts/qa/pace' -Wait
Start-Process $exe -ArgumentList '--reset-self-test artifacts/qa/reset' -Wait
Start-Process $exe -ArgumentList '--overlay-order-self-test artifacts/qa/overlay.txt' -Wait
Start-Process $exe -ArgumentList '--input-relay-self-test artifacts/qa/drag.json' -Wait
Start-Process $exe -ArgumentList '--language-self-test artifacts/qa/language' -Wait
```

Self-test arguments are handled before the single-instance check and do not load personal quota. Language, window-order and mouse-relay checks briefly create test windows and need an interactive Windows desktop; avoid interacting with those windows during the test.

The test suites cover layout and pointer mapping, quota parsing, pace and prediction, reset-signal freshness/deduplication, native hit targets and overlay restoration. Language checks cover translation format arguments, live switching, saved preferences, bilingual input, notification identity and English layout. The language test also renders example screenshots.

GitHub CI builds the package and runs layout and reset-policy checks. Desktop interaction checks must also be run locally.

## Code map

| File | Responsibility |
| --- | --- |
| `CodexPetLimitRings.Windows/UiText.cs` | English translations, formatting culture and live XAML translation bindings |
| `CodexPetLimitRings.Windows/WeeklyPacing.cs` | Pace calculations, predictions and bilingual paste parsing |
| `CodexPetLimitRings.Windows/MainController.cs` | Polling, Pet tracking, tray behavior and language application |
| `CodexPetLimitRings.Windows/Services/UsageService.cs` | Personal quota reads |
| `CodexPetLimitRings.Windows/Services/ResetSignalService.cs` | Public signals, freshness validation and deduplication |
| `CodexPetLimitRings.Windows/Interop/NativeMethods.cs` | Win32 window discovery, pointer forwarding and window order |
| `CodexPetLimitRings.Windows/Views/` | HUD, details and settings |

Use `UiText.T` for labels, `UiText.F` for formatted messages, and `{local:Tr 'Chinese source text'}` for XAML labels that must update immediately. Add the English text to `UiText.cs`. Keep internal status keys and event identities independent of language; do not translate user input or upstream announcement content.

The saved `language` setting accepts `zh-CN` or `en`. Existing configurations without this field retain the Chinese default. Changing language preserves all other settings; restoring layout defaults preserves the selected language.

Codex's state format, window structure and quota endpoint may change. Windows x64 is the validated target. System-menu handling recognizes common Windows 10/11 tray flyouts, native menus and some shell panels; other third-party overlays rely on normal window order.

Logs: `%LOCALAPPDATA%\CodexWeeklyPetHud\Logs\runtime.log`. Do not commit personal settings, logs, `auth.json` or tokens. Set `CODEX_HOME` for a custom Codex data directory.
