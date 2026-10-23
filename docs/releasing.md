# Weekly HUD release handbook

This fork publishes **Windows x64 only**, using `scripts/release/package-weekly.ps1`. The upstream multi-platform process below is retained for reference; `publish-release.ps1` and `verify-release.sh` target the upstream project and are not the release commands for this fork.

## Windows x64 release flow

1. Update the project version, the packaging default and the Windows workflow tag together. Add a dated changelog section and update both root READMEs, download examples and sample screenshots.
2. Build and run the layout tests plus the packaged executable's reset, pacing and bilingual UI self-tests. Validate taskbar rendering on an interactive Windows desktop; record the actual OS build and scaling in the release notes.
3. Run the tracked-source secret scan and review the diff. Commit the intended changes, push `main` and wait for the Windows build workflow to pass.
4. From that clean commit, run `pwsh -NoProfile -File scripts/release/package-weekly.ps1 -Tag vX.Y.Z`. Check that the ZIP contains only the executable, runtime libraries, documentation, sample images and licenses. Confirm its SHA-256 matches `dist/SHA256SUMS.txt`.
5. Create and push an annotated version tag at the tested commit. Create a draft GitHub Release with the ZIP and `SHA256SUMS.txt`, verify both uploads, then publish it as the latest release. Use a UTF-8 notes file with `gh release create --notes-file`; include Chinese and English changes, validation limits and upgrade instructions.
6. Read back the public latest release, tagged commit and exact two-asset set. Verify the published ZIP digest and checksum file before considering the release complete.

The website discovers the latest release dynamically. No version-specific website rebuild is needed. Windows builds remain unsigned; retain that information in the download documentation.

Expected assets:

```text
Codex-Weekly-Pet-HUD-vX.Y.Z-Windows-x64.zip
SHA256SUMS.txt
```

## Upstream reference (not used by this fork)

Codex Pet HUD publishes unsigned preview builds from version tags. The tag is the single version source for both native apps.

## Release flow

1. Update `CHANGELOG.md`, move changes from `Unreleased` into a dated version, and advance the `[Unreleased]` comparison link to that tag.
2. Run `./scripts/release/scan-secrets.sh`, `./scripts/test-macos.sh`, and `./scripts/release/verify-release.sh vX.Y.Z`, then verify macOS locally with `./scripts/release/build-macos.sh vX.Y.Z`.
3. Commit and push `main`, then require a clean worktree whose `HEAD` matches `origin/main`.
4. Run `pwsh -NoProfile -File scripts/release/publish-release.ps1 -Tag vX.Y.Z`. This is the required finish step: it repeats local preflight checks, builds the current platform packages, creates and pushes the annotated tag, waits for the Release workflow, and verifies the public latest release and exact asset set.
5. The Release workflow first rejects tag, changelog, generated-note, comparison-link, or website-contract drift, then builds macOS universal, Windows x64, and Windows ARM64 archives in clean GitHub runners.
6. The publish job rejects forbidden files, writes `SHA256SUMS.txt`, verifies the checksums, and builds categorized release notes from that version's `CHANGELOG.md` section.
7. The workflow creates the latest GitHub Release marked **Unsigned Preview** and verifies that GitHub exposes the tag as the latest release.
8. Treat the release as complete only when `publish-release.ps1` reads back all three archives plus `SHA256SUMS.txt`, uploaded with SHA-256 digests and tied to the tagged commit.

Audit an already-published version without mutation:

```powershell
pwsh -NoProfile -File scripts/release/publish-release.ps1 -Tag vX.Y.Z -VerifyOnly
```

The project website reads GitHub's latest non-draft, non-prerelease Release directly. Publishing a release updates its version, publication date, downloads, and categorized changelog without another website edit or deployment.

## Expected assets

```text
Codex-Pet-HUD-vX.Y.Z-macOS-universal.zip
Codex-Pet-HUD-vX.Y.Z-Windows-x64.zip
Codex-Pet-HUD-vX.Y.Z-Windows-arm64.zip
SHA256SUMS.txt
```

GitHub automatically supplies source archives for the tagged commit.

## Signing gate

The current harness uses an ad-hoc macOS signature and unsigned Windows binaries. Releases must remain marked **Unsigned Preview** until all of these are true:

- macOS Developer ID signing, hardened runtime, notarization, and stapling pass
- Windows Authenticode signing passes `signtool verify /pa`
- Windows x64 and ARM64 UI, DPI, tray, startup, and uninstall behavior pass on real devices
- Both archives are rechecked for `.codex`, `auth.json`, logs, credentials, certificates, and local paths

Do not document Gatekeeper or SmartScreen bypasses as the normal installation path.
