# CODEX PERISCOPE v5.2.0 — Windows stable

**Codex Periscope** is an independent native Windows quota and local activity radar for **Codex quotas attached to a ChatGPT account**. It does not monitor general ChatGPT chat usage or API billing and is **not affiliated with OpenAI**.

## What's new in 5.2
- **Obsidian Sonar visual design:** redesigned Circular, Vertical, Compact and Micro layouts with larger native radar lenses, more readable quota gauges and carefully differentiated degraded states.
- **Multi-task view:** independent radar cards and elapsed durations for several *safely observed* local Codex turns. The end of one task does not stop another's display.
- **Quota History V2:** time axis and percentage axis, 5H/weekly series, period filters, tooltips, accessible readings, observed versus expected reset markers, and explicit gaps/invalid values.
- **Notification and icon improvements:** confirmed terminal-event notifications with opt-in/deduplication; native taskbar/Alt+Tab/History icons and separate non-animated quota tray icon.
- **Read-only source and freshness status:** stale/blocked/unavailable quota readings are not presented as current; ambiguous local task activity remains UNVERIFIED.
- Settings/history compatibility with V5.1, optional startup, no Electron, server or paid model calls.

## Download / verify
Official Windows package asset: **CODEX_PERISCOPE_V5_2_WINDOWS.zip**.
The preview artifact called **CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip** is **not** a stable Release asset and must not be promoted by simply renaming it.

The package includes:
- SHINO-CODEX-MONITOR.exe (retained technical filename for compatibility)
- CODEX-PERISCOPE.ico
- README-EN.txt
- INSTALL-PERISCOPE-V5.2.bat and .ps1
- UNINSTALL-PERISCOPE-V5.2.ps1
- SHA256SUMS.txt

Download only from the project's GitHub Releases page. Cross-check the published asset SHA-256 digest with GitHub's release asset metadata where available, and the packaged executable checksum with SHA256SUMS.txt.

## Install / upgrade
1. Exit the running widget completely from the notification area.
2. Extract the stable ZIP to a dedicated folder.
3. Run INSTALL-PERISCOPE-V5.2.bat. The installer validates version, checksum and embedded application/activity tests before replacing the old EXE.
4. Local preferences and history are preserved, and the previous executable is backed up. Windows autostart is never silently enabled or migrated.
5. Windows may show security warnings because this program is **unsigned**. Do not disable Defender, SmartScreen or organizational protections.

For a portable test, extract and run SHINO-CODEX-MONITOR.exe without installing; the application can still read existing local preferences/history.

## Remove / rollback
Use Windows Installed Apps or the Start Menu uninstall shortcut. The uninstaller requires confirmation and retains settings/history by default. See README-EN.txt and the repository's RELEASE-GATE-V5.2.md for the verified rollback expectations.

## Data-source limitations
The quota source uses an already authenticated local Codex CLI. Task status is inferred only from **undocumented, read-only local Codex JSONL**. It cannot reliably identify every active Desktop tab; missing or ambiguous evidence displays **UNVERIFIED**, not a fabricated completion. Quota timestamps and reset times are only shown when supplied and validated.

This software does not ask for ChatGPT passwords or tokens and makes no paid AI inference calls.

## Acceptance and distribution
Owner-reported Windows field verification of the **074b41f preview** covered: real appearance, two concurrent missions, notifications enabled/disabled without duplicates, taskbar/Alt+Tab icons, sleep/wake and mixed-DPI monitor transfers, preferences/history plus rollback, and same-account dashboard quota comparison at the same moment. These are user reports, not independently reproduced by CI.

**Stable promotion requires a new build with the final 5.2.0 assembly identity, passing Windows CI and release download/package verification.** Release publication also requires explicit maintainer authorization.

Sources: [PR #14](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14) · [Release acceptance](https://github.com/shinobione/SHINO-CODEX-MONITOR/blob/main/RELEASE-GATE-V5.2.md)
