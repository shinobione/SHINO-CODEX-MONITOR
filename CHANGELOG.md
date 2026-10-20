# Changelog

## 5.2.0-preview.1 — Codex Periscope (not released)

- **Brand:** Codex Periscope / Quota & Activity Radar, an independent Windows app solely for Codex.
- **Interface:** English-only menus, status text, tooltips, dialogs and task/quota diagnostics.
- **Radar look:** generated multi-resolution native Windows icon, concentric activity dial with teal sweep and contact blip.
- **Migration:** preserves V5.1 settings/history and internal installation paths; opt-in migration of legacy V3/V4 startup entries with checks against unrelated keys.
- **Distribution:** dedicated 5.2 preview build and installer scripts. The installer validates assembly version, SHA-256, 98 quota tests and 46 task tests before replacement, and preserves the previous executable for rollback.
- **Release gate:** a successful Windows CI run, real upgrade/rollback tests and user approval are required before any merge or public release.

## 5.1.0 — SHINO // CODEX MONITOR (published October 19, 2026)

- Integrated Codex local task observation beneath quota gauges in one native widget.
- Added a lightweight animated execution icon, confirmed completion state, model and effort when observed.
- Added stale/unverified states and a privacy-safe local diagnostic.
- Added source-aware turn duration and child-session filtering when metadata is exposed.
- Introduced a dedicated Windows installable ZIP and 98+46 C# self-test gates.
- Important limit: undocumented JSONL data cannot reliably identify every active Codex Desktop tab.

## 5.0.0 — Stable quota monitoring

- Native WinForms widget for Codex account usage; 5-hour and weekly windows when actually exposed.
- Reset times, manual/automatic refresh, data freshness and source health.
- Tray, optional pinned layouts, alerts and local history.

Prior release details remain available in the Git commit history and V5.1 release notes.
