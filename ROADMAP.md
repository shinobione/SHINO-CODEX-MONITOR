# CODEX PERISCOPE — Roadmap

**Status:** V5.2.0-preview.1 development. Public stable release remains SHINO Codex Monitor V5.1.0.

## V5.2 rebrand goals

- [x] Adopt name **CODEX PERISCOPE** and radar/sonar design direction.
- [x] Introduce a new Windows radar icon with 16/32/48/64/128 px variants and animated native GDI+ activity sweep.
- [x] Translate core UI, alerts, diagnostic source reports, history and lifecycle labels into English.
- [x] Set English culture for display strings and date names, without changing local timezone.
- [x] Add a dedicated versioned installer and uninstaller; preserve legacy data and installation paths invisibly for upgrades.
- [x] Require user confirmation before moving an existing Windows startup entry; never touch unrelated registry entries.
- [ ] Verify the **current branch** through Windows CI: 98 core tests, 46 task tests, English UI/installer tests, build and preview package dry-run.
- [ ] Verify V5.1 → V5.2 upgrade on a real Windows installation, startup migration, rollback, uninstall and old Start Menu shortcuts.
- [ ] Test UI at 100/125/150/200% display scaling and multiple monitors.
- [ ] Confirm Codex Desktop task durations, notification behavior and quotas against the official dashboard on the same account.
- [ ] Test upgrade checking from published V5.1: legacy asset compatibility will require a deliberately supported migration package; do **not** publish an unverified broken update path.
- [ ] Obtain approval before merging into main or publishing a V5.2 Release.

## Future

Investigate official documented Codex Desktop status subscription if one becomes available. Do not attach to private app-server instances or extract credentials. Keep CPU/RAM usage low and local/offline UI responsive.

## Non-goals

No chat-classic quotas, API billing dashboards, multi-provider monitoring, secret extraction, server, Electron or mandatory hosting.

**Repository:** [SHINO-CODEX-MONITOR](https://github.com/shinobione/SHINO-CODEX-MONITOR) (repository kept for existing update links). **V5.2 issue:** [#12](https://github.com/shinobione/SHINO-CODEX-MONITOR/issues/12).
