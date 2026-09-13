# Contributing to Codex Weekly Pet HUD

Thanks for helping the tiny HUD stay focused and reliable.

## Before opening a pull request

1. Keep changes scoped to one behavior or platform concern.
2. Never add keyboard hooks, token logging, or cleanup outside app-owned paths.
3. Preserve Pet visibility tracking and yield overlay input to system flyouts. Reset radar checks continue while the Pet is hidden.
4. Follow the build and test commands in `platforms/windows/README.md`; verify interaction changes on an interactive Windows desktop.
5. Update the root Chinese README and Windows documentation for changed behavior. Other translations and macOS files are retained upstream references.

Bug reports should include the OS version, Codex version, expected behavior, and sanitized logs. Never attach `auth.json` or access tokens.
