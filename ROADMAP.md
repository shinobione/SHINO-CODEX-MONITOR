# CODEX PERISCOPE — Product Roadmap

Last updated: October 19, 2026 (Windows installation and colors/position/history preservation confirmed by user).

## Version history

| Release | Status | Notes |
|---|---|---|
| V5.1.0 | **Published stable** | SHINO // CODEX MONITOR — baseline for upgrades |
| V5.2.0-preview.1 | **Draft, not published** | Codex Periscope radar branding, English-only UI, migration safety |
| V5.2.0 stable | **Pending tests and approval** | Dedicated Windows installer, verified V5.1 upgrade and rollback |

## Acceptance checklist for V5.2

- [x] Product name selected: **CODEX PERISCOPE**.
- [x] Scope stays exclusive to Codex ChatGPT quota monitoring and locally observable Codex tasks.
- [x] Independent community-app disclaimer documented.
- [x] GDI+ radar icon generator and native activity sweep added.
- [x] Initial English translation of UI, menus, diagnostics and startup prompts.
- [x] Separate V5.2 preview build and installer sources added; no automatic publication.
- [x] **Automated English-only audit** on application, history, notification and update strings; complete real Windows screen review still pending.
- [x] Windows CI on the last code commit tested before this documentation update: [run #39313221295](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/runs/39313221295), 109 Python tests, 98 application tests, 46 task tests, native icon, and installer `-ValidateOnly`.
- [x] Real Windows installation of the **V5.2 Preview confirmed by the user on October 19, 2026**. Installation succeeded after **four Windows confirmation prompts**. Their types and origins have not yet been identified; installation was not independently observed by CI or this assistant.
- [ ] Reduce/clarify the **four Windows confirmation prompts** after identifying each one. Keep Windows security protections; do not suppress UAC, Defender or SmartScreen.
- [x] **User-confirmed V5.1 → V5.2 data preservation:** custom colors, widget position and quota history remained intact after installation (Windows PC, October 19, 2026).
- [ ] Independently verify remaining upgrade aspects: tray/taskbar icon, Start Menu shortcuts, startup registry migration and behavior after Windows reboot.
- [ ] Real rollback/uninstall test with no loss of user data.
- [ ] DPI 100–200 %, multiple monitors, sleep/wake, offline behavior, long Codex turn, duration and status comparison.
- [x] **Radar ICO GDI+ pixel validation** passed at 16, 32, 48 and 256 px using the native Windows CI runner.
- [x] Notification source logic hardened: ignore previously completed or future terminal events; enforce both opt-in switches.
- [ ] Real notification delivery and radar animation/legibility on the user's Windows PC remain to be observed.
- [ ] User approval before merging to main, renaming the repository, creating a tag or publishing a Release.

## Known limitations

- Local Codex JSONL logs are undocumented and cannot reliably identify the active Codex Desktop tab.
- The CLI quota source is distinct from the activity source. A stale observation must never appear current.
- The current EXE and local-data filenames are temporarily retained for backward compatibility.
- Unsigned Windows binaries may prompt security warnings. Never bypass OS security policy.

[Draft PR #14](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14) · [Release acceptance report](RELEASE-GATE-V5.2.md) · [Migration issue #12](https://github.com/shinobione/SHINO-CODEX-MONITOR/issues/12).
