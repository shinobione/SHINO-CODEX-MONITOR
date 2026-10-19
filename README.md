# CODEX PERISCOPE

**An independent Windows radar for Codex quotas and task activity.** All UI text, menus, installers, alerts, history and diagnostics are **English-only**, regardless of Windows display language.

**Version in development:** `5.2.0-preview.1` · **Stable published version:** [SHINO Codex Monitor v5.1.0](https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.1.0). Codex Periscope is a rebrand of that same application, not a new cloud service.

> Independent community utility, **not affiliated with or endorsed by OpenAI**. It reads **Codex limits on a ChatGPT account**, not classic ChatGPT chat limits or OpenAI API spending.

## Features

- **Quota radar:** remaining quota, 5-hour and weekly windows only when exposed, next reset, last update, confidence and source status.
- **Local Codex activity:** animated rotating radar beam while a user turn is observed, green confirmation after its recorded completion, elapsed duration, model and effort when provided. No invented progress percentage.
- **Native Windows:** lightweight C# WinForms, a tray icon, an optional taskbar entry, pinning, opacity, four layouts, support for multiple displays and display scaling.
- **Notifications:** configurable quota alerts, completed-turn alerts, Windows notification test. Delivery still depends on Windows settings.
- **Privacy:** no cookies, token extraction, ChatGPT password prompt, background server or paid inference call.

### Important telemetry distinction

| Metric | Source | Confidence |
|---|---|---|
| ChatGPT Codex quotas | Authenticated Codex CLI `account/rateLimits/read` | Source timestamped and marked stale when necessary |
| Current turn activity | Local Codex JSONL session files | **Indicative**, undocumented format; not guaranteed to track the active Desktop tab |
| Model / reasoning effort | Local turn metadata when exposed | Explicitly not provided if missing |
| Finished turn duration | Verified start/end timestamps or consistent `duration_ms` | Only applies to the identified local user turn |

**No record is not an idle signal.** If task telemetry cannot verify activity, Codex Periscope says **UNVERIFIED**, never falsely reports a finished active task.

## V5.2 preview (Windows)

On [the V5.2 pull request](https://github.com/shinobione/SHINO-CODEX-MONITOR/pulls), open the latest successful Windows preview workflow and download `CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip`.

1. Exit the old SHINO Codex Monitor from the tray.
2. Extract the ZIP and run `CODEX-PERISCOPE.exe` directly. No admin rights.
3. Check the embedded **5.2.0 PREVIEW** version, Codex quota freshness, radar animation, windows/taskbar icon, and English menus.
4. For an installation test, run `INSTALLER-V5.2.bat`. It verifies SHA-256 and runs the program's 98 core plus 46 task tests before installing. The installer is **not** run automatically.
5. For removal, use Windows Installed Apps or `UNINSTALL-V5.2.ps1` and type **YES**. Local settings and quota history are preserved.

**Build locally (Windows):** `BUILD-PREVIEW-V5.2.bat`. This creates the portable archive and runs installer `-ValidateOnly` (no installation).

**Unsigned preview.** SHA-256 verifies contents, not publisher identity. Do not disable Windows Defender or SmartScreen to run it.

## Upgrade compatibility: formerly SHINO // CODEX MONITOR

To protect existing V5.1 installations, the new installer keeps the internal legacy application directory and local data folder:
`%LOCALAPPDATA%\Programs\SHINO Codex Monitor` and `%LOCALAPPDATA%\SHINO-CODEX-MONITOR`.
Those are **backward-compatible internal paths**, not the public branding. The displayed product, Windows shortcut, tray label, taskbar icon and new executable use **Codex Periscope**.

Windows autostart stays optional. If a legacy entry is detected, the installer prompts before migrating it. The widget also offers explicit, reviewed migration on the **Start with Windows** option; it never silently edits an unknown startup entry. The Windows single-instance mutex is retained to prevent two monitor instances.

The GitHub repository and current stable Release channel retain their original URL during the migration. V5.2 is **not published** until its real Windows and update-path tests are confirmed.

## English menus

| Menu | Purpose |
|---|---|
| Show / hide, Refresh quotas | Quick actions |
| Codex activity (local) | Refresh, root-thread source detail and sanitized diagnostics |
| Quotas & data | Official usage dashboard, collection freshness and reset options |
| Appearance | Gauge layouts, theme, opacity, radar animation and tray icon |
| Window | Pin, taskbar visibility, always on top and screen edge snapping |
| Notifications | Quota thresholds, task completion, Windows notification test |
| History | View stored real quota samples |
| Application | Stable update check, optional Windows startup, support diagnostics, exit |

## Troubleshooting and remaining limitations

No quotas? Verify that the Codex CLI is installed and logged into the **same ChatGPT account** as the dashboard. No task activity? Check the sanitized local diagnostic; Codex Desktop may not persist all turns in the local JSONL source. No notifications? Check Windows notifications and Do not disturb. No taskbar icon? Toggle Window → Show in taskbar and refresh any pinned shortcut.

For a tested release, requirements and open tasks see [ROADMAP.md](ROADMAP.md). The [CHANGELOG.md](CHANGELOG.md) describes preview changes. Historical stable documentation is available in the [v5.1.0 tag](https://github.com/shinobione/SHINO-CODEX-MONITOR/tree/v5.1.0).
