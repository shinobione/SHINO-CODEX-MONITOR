CODEX PERISCOPE V5.2.0 PREVIEW
==============================
Independent Windows radar for Codex ChatGPT quotas and locally observable tasks.
NOT AN OFFICIAL OPENAI APPLICATION.

How to try:
1. Exit the previous Codex Monitor completely from the Windows system tray.
2. Run CODEX-PERISCOPE.exe directly from this extracted folder.
3. The app remains local and does not require administrator rights.
4. Compare a real Codex Desktop turn against the "Codex Activity Radar" display.
   Task state comes from local undocumented JSONL sessions and is not guaranteed.

To install instead:
Run INSTALLER-V5.2.bat. It first verifies the version and SHA-256 and
runs 98 quota and 46 task self-tests. It uses the existing installation
directory to preserve settings and quota history. If a legacy Windows
startup entry is found, it asks before migrating it.

To uninstall:
Use Windows Installed Apps or UNINSTALL-V5.2.ps1. Type YES to confirm.
User preferences and quota history remain under
%LOCALAPPDATA%\SHINO-CODEX-MONITOR (legacy internal data path).

UI LANGUAGE: English only. Dates/times are displayed in local time.
QUOTA SOURCE: existing authenticated Codex CLI account/rateLimits/read.
TASK SOURCE: local JSONL sessions, partial and undocumented.
Never interpret missing task telemetry as an idle Codex Desktop session.
Never enter ChatGPT credentials in the widget.
No cookies or tokens extracted. No paid AI requests are used.
Unsigned test build: never disable Windows security protections.

Formerly SHINO // CODEX MONITOR. GitHub repository kept unchanged
for updater/history compatibility while the rename is validated.
https://github.com/shinobione/SHINO-CODEX-MONITOR
