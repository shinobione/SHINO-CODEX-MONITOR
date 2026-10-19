# CODEX PERISCOPE — Product Roadmap

Last updated: October 19, 2026.

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
- [ ] **English-only audit** across all actually displayed strings, notifications, self-tests and installation/uninstallation.
- [ ] Green Windows CI on the exact candidate commit: 101+ Python tests, 98 quota tests, 46 task tests, icon, installer `-ValidateOnly`.
- [ ] Real test of V5.1 → V5.2 preserving settings, quota history, tray, Windows shortcuts and safe startup migration.
- [ ] Real rollback/uninstall test with no loss of user data.
- [ ] DPI 100–200 %, multiple monitors, sleep/wake, offline behavior, long Codex turn, duration and status comparison.
- [ ] Verify Windows notification delivery and radar rendering at 16, 32, 48 and 256 px.
- [ ] User approval before merging to main, renaming the repository, creating a tag or publishing a Release.

## Known limitations

- Local Codex JSONL logs are undocumented and cannot reliably identify the active Codex Desktop tab.
- The CLI quota source is distinct from the activity source. A stale observation must never appear current.
- The current EXE and local-data filenames are temporarily retained for backward compatibility.
- Unsigned Windows binaries may prompt security warnings. Never bypass OS security policy.

[Draft PR #14](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14) · [Migration issue #12](https://github.com/shinobione/SHINO-CODEX-MONITOR/issues/12).
