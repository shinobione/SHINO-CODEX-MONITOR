CODEX PERISCOPE 5.2.0 — WINDOWS STABLE
=====================================

Independent Windows quota and activity radar for Codex.
Not affiliated with OpenAI.

INSTALL / UPGRADE
1. Download CODEX_PERISCOPE_V5_2_WINDOWS.zip only from this repository's
   official GitHub Release v5.2.0 and verify SHA-256 if possible.
2. Exit the previous widget completely using its system tray menu.
3. Extract the ZIP to a dedicated directory.
4. Run INSTALL-PERISCOPE-V5.2.bat to validate and install.
   Its PowerShell launcher uses process-only ExecutionPolicy Bypass for this
   unsigned script; it does NOT change the persistent execution policy or
   disable Defender, SmartScreen or organization-managed restrictions.
   Stop if Windows or organization policy blocks execution.
5. The installer checks the EXE hash and version, and runs embedded tests
   before replacing a previous executable. The prior EXE is backed up.
6. The installer preserves local settings/history and does not configure
   startup automatically. Restart the application after a successful install.

FEATURES
- 5-hour and weekly Codex quota remaining values and known reset timestamps.
- Quota source validity, freshness, color thresholds and native tray icon.
- Native Obsidian sonar activity radar for safely observed local Codex turns.
- Multiple independent observed turns, when local files are unambiguous.
- Quota History V2 with dates, axes, periods, gaps and accessible data table.
- Optional notifications, four widget layouts, multi-display window placement.

DATA & LIMITATIONS
- Quotas use the existing authenticated Codex CLI on the same ChatGPT account.
- Local Codex JSONL turn files are UNDOCUMENTED and may be incomplete.
- UNVERIFIED is not equivalent to idle or successful completion.
- No ChatGPT credentials are requested by this application.
- No API inference fees, web runtime or recurring service dependency.
- Binary is UNSIGNED; SHA-256 validates bytes, not publisher identity.

WINDOWS STARTUP
Pre-existing known entries are preserved. Changes/migration require consent
in the app's Startup controls. Do not configure automatic startup blindly.

UNINSTALL AND RECOVERY
Use Windows Installed Apps or the Start Menu uninstall shortcut; type YES
when prompted. Settings/history are preserved by default.
The app retains compatibility paths under:
%LOCALAPPDATA%\SHINO-CODEX-MONITOR
%LOCALAPPDATA%\Programs\SHINO Codex Monitor
A prior executable backup is retained by the upgrade installer.

Do not disable Windows protections to run this software.
Report defects: https://github.com/shinobione/SHINO-CODEX-MONITOR/issues
Release: https://github.com/shinobione/SHINO-CODEX-MONITOR/releases/tag/v5.2.0
