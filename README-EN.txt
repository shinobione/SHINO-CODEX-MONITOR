CODEX PERISCOPE 5.2.0 PREVIEW — WINDOWS
=======================================

An independent quota and activity radar for Codex.
Not affiliated with OpenAI.

IMPORTANT: THIS IS AN UNSIGNED PREVIEW, NOT A STABLE RELEASE.
Use only the artifact created by the verified Windows GitHub Actions run.

INSTALL / UPGRADE FROM V5.1
1. Exit the running monitor completely from the system tray.
2. Extract the archive to a separate directory.
3. Run INSTALL-PERISCOPE-V5.2.bat. The launcher starts a temporary
   PowerShell process with -ExecutionPolicy Bypass because this preview
   installer is not digitally signed. This does NOT change the persistent
   user/machine execution policy and does NOT disable Defender or SmartScreen.
   If your organization enforces Group Policy, stop and ask your administrator.
   Do not bypass organization-managed restrictions.
4. The installer checks SHA-256, executable version 5.2.0, and 98 + 46
   embedded C# tests BEFORE touching the installed application.
5. Your existing settings, history and install location are preserved.
   The previous EXE is backed up for rollback.

BRAND & LANGUAGE
All application UI, dialogs, notifications and installation prompts
are English. Codex Periscope uses a native radar lens icon, dark navy
surface and teal activity sweep with distinct quota warning colors.

IMPORTANT LIMITATIONS
Quota data: Codex CLI already signed in with the same ChatGPT account.
Task data: read-only local Codex JSONL logs (undocumented and incomplete).
UNVERIFIED is not proof that Codex is idle. The active Desktop tab cannot
be matched with a certainty from local logs. Never enter your ChatGPT
password in this widget. No model API calls or recurring hosting fees.

START WITH WINDOWS
Legacy V3/V4 startup values are preserved until you explicitly migrate
them using the in-app Startup menu. Unknown values are never removed.

REMOVE / ROLLBACK
Use Windows Installed Apps or the Start Menu Uninstall Codex Periscope
shortcut. Confirm by typing YES. Local settings and quota history
remain by default. To revert from a portable test, exit and delete its
folder, then relaunch the installed V5.1 release.

SECURITY
Unsigned software may show a Windows security prompt.
Do not disable Defender, SmartScreen or Windows security policies.

SUPPORT
https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14
