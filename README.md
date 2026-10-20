# CODEX PERISCOPE

**Quota & Activity Radar for Codex — native Windows application.**

**Independent community software — not affiliated with OpenAI.** Codex Periscope only tracks **Codex quotas attached to a ChatGPT account** and the local Codex activity it can actually observe. It does **not** monitor general ChatGPT chat quotas, API billing, Claude, Gemini or other services.

| Channel | Version | Status |
|---|---|---|
| **Stable** | [5.1.0](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.1.0) | Published as SHINO // CODEX MONITOR |
| **Brand preview** | **5.2.0-preview.1** | [Draft PR #14](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14), not published |
| **Unpublished release candidate** | **5.2.0** | [Draft PR #16](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/16), Windows CI pending; **not a public GitHub Release** |

For the V5.2 publication decision, see the [radar, Windows notifications and release acceptance report](RELEASE-GATE-V5.2.md). **Windows CI and GDI+ icon pixel tests have passed; live Windows toast delivery, reboot/autostart, multi-DPI UI and rollback remain field checks.**

## What the widget shows

- **5-hour and weekly Codex quotas:** remaining percentages, resets only when supplied, timestamp, source status and staleness.
- **Local activity:** a teal radar sweep for a recently observed primary user turn, a green check for a confirmed completion event, and **UNVERIFIED** whenever events are missing, old or ambiguous.
- **Duration, model and effort** only when present in the local session data; never infer a reliable percent of task completion.
- Compact WinForms widget, circular gauges, optional pinned window, tray indicator, history, configurable alerts, multi-monitor placement and four layouts.
- No Electron, server, hosting or paid AI model calls.

### Data-source boundaries

| Feature | Source | Reliability |
|---|---|---|
| Quotas | Existing authenticated Codex CLI (`account/rateLimits/read`) | Time-stamped, read-only |
| Activity | Local Codex JSONL session files | **Indicative, undocumented; not guaranteed to match the active Codex Desktop tab** |

Silence in a log does **not** prove a task is finished. When evidence is insufficient, the widget must say **UNVERIFIED** instead of showing a fake green check or duration.

## English-only user experience

Every user-facing UI label, reset line, dialog, tooltip, notification, installer/uninstaller message and diagnostic is intended to be **English only**. The V5.2 Preview is being audited for remaining legacy strings. Historical internal filenames and installation paths are retained temporarily to avoid breaking V5.1 upgrades; they are not product branding.

## Windows V5.2 preview

Use the **CODEX-PERISCOPE-V5.2-PREVIEW-WINDOWS** artifact from the latest successful PR #14 Windows workflow. If the CI is not green, **do not install this candidate**.

1. Fully exit the previous application using its system-tray menu.
2. Extract `CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip`.
3. For a no-install test, run the included `SHINO-CODEX-MONITOR.exe` directly; the internal executable filename remains for migration compatibility.
4. For an in-place upgrade after CI approval, run `INSTALL-PERISCOPE-V5.2.bat`. Its validator checks SHA-256, the expected 5.2 assembly version, and 98 + 46 C# self-tests **before replacing the current executable**.
5. Keep the existing settings and quota history. The previous executable is backed up. Windows startup changes require separate confirmation inside the application.

**Security:** the binary is unsigned. Do not disable Defender, SmartScreen or Windows security policies.

## Build locally (Windows)

```powershell
.\BUILD-PERISCOPE-V5.2.ps1
```

The build generates a multi-resolution radar icon from local System.Drawing code, compiles the WinForms EXE and verifies the package without installing anything.

For source audits, run `python -m unittest discover -s tests -p "test_*.py"`. The source tests include synthetic lifecycle cases; they **do not** prove real-time accuracy on all Codex Desktop versions.

## UI and settings

The tray menu groups **Codex Activity**, **Quotas & Data**, **Appearance**, **Window**, **Notifications**, **History**, and **Application**. Quota refresh and local-task refresh are independent. Task animation runs only while the widget is visible.

The Windows startup migration recognizes legacy V3/V4 app entries, refuses to delete unknown commands, and offers an explicit migration prompt. No autostart is enabled by installation without permission.

## Removal and rollback

Use Installed Apps or the **Uninstall Codex Periscope** Start Menu shortcut. The uninstaller asks for `YES` and preserves local settings/history by default. If you are only testing the portable version, exit it and delete the extracted folder.

Existing user data is stored under `%LOCALAPPDATA%\SHINO-CODEX-MONITOR` for compatibility. The installed application path remains under `%LOCALAPPDATA%\Programs\SHINO Codex Monitor` during the staged migration. These technical names will be migrated only when a safe rollback path is proven.

## Links

[Roadmap](ROADMAP.md) · [Changelog](CHANGELOG.md) · [Brand brief](BRANDING-V5.2.md) · [Windows startup and migration issue](https://github.com/shinobione/SHINO-CODEX-MONITOR/issues/12)

**V5.1.0 remains stable until V5.2 migration and Windows tests pass.**
