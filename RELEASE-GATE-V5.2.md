# CODEX PERISCOPE V5.2 — Pre-release acceptance report

**Scope:** native Windows app for ChatGPT-account Codex quota monitoring and observable local Codex tasks. No general ChatGPT or API usage meter. Independent software, not affiliated with OpenAI.

**Premium candidate continuation:** see [Premium Polish acceptance, History V2 guide and performance report](PREMIUM-POLISH-V5.2.md). The mission supplies five additional baseline owner field confirmations; these are recorded there separately from candidate synthetic tests. Stable release remains **HOLD** pending candidate field checks and explicit promotion approval.

**Release-blocking P0 continuation:** [Concurrent-turn collection, native window icon checks and field gate](P0-MULTI-TASK-V5.2.md). Unit/CI success alone cannot clear the real concurrent-mission and shell taskbar visual gates.

**October 20 native redesign:** [Stitch Obsidian -> full-window WinForms candidate](OBSIDIAN-NATIVE-V5.2.md) addresses issue #15 with complete native fixture captures. Fresh owner screenshot approval is required; the earlier visual failure is not cleared by synthetic QA or CI. PR #14 stays Draft and stable Release stays HOLD.

**Status (October 19, 2026):** V5.2 Preview, PR #14 remains **Draft**; main and published V5.1 are unchanged. No automatic installation, autostart, merge, public tag or Release was performed for this audit.

## Verified in source and Windows CI

- [x] Radar rendering uses native System.Drawing, no Electron, GDI+ control with concentric rings, crosshair, moving teal sweep and static task-state markers. Animation timer only runs while active, visible and explicitly enabled.
- [x] **Actual ICO raster frames** decoded and color-sampled by `TEST-PERISCOPE-RADAR.ps1`: 16, 32, 48 and 256 pixels each contain visible pixels and teal radar pixels. Embedded icon is used by the Windows main window and history window.
- [x] Quota tray progress ring remains distinct from the teal active-task dot and does not animate continuously.
- [x] Quota alerts require an enabled main notification switch, per-window setting and a confirmed threshold crossing or reset. A single incomplete sample does not trigger a quota warning.
- [x] Task completion alerts require the main notification switch AND the task setting, an identified local turn with a confirmed terminal event, sane event times and two consistent reads. Startup/replayed/old/future terminal events are rejected; no claim that the Codex Desktop tab is always identified.
- [x] Explicit **Test Windows notification** action is available even when alert settings are disabled, to diagnose operating system delivery.
- [x] English localization corrections: history view/title/ranges, task state and tooltip, wake diagnostics, update handoff/error strings. Internal legacy install/data paths remain unchanged for migration compatibility.
- [x] Legacy V5.1 publisher restricted to manual dispatch so it cannot fail the V5.2 merge or silently republish V5.1.
- [x] Native Windows CI compiles EXE, runs Python safety/UI/English audits plus 98 application and 46 synthetic activity self-tests, checks installer integrity in `-ValidateOnly`, and generates an **unsigned Preview ZIP**.

## Confirmed on user's actual Windows PC

- [x] V5.2 Preview installer completed after **four Windows confirmation dialogs** (exact dialogs unknown).
- [x] V5.1 to V5.2 migration preserved custom colors, window position and local quota history.

## Remaining field acceptance gates — not yet tested

- [ ] Visually inspect radar sweep and text legibility on user's actual DPI/scaling and all four layouts (16 px tray; native EXE/taskbar; history icon; 100–200% DPI / external monitors).
- [ ] Trigger **Notifications → Test Windows notification** and confirm a visible Windows notification. Test actual task completion with both notification toggles enabled, and a task while alerts are disabled (must be silent). Check duplicate alerts after restart/sleep.
- [ ] Confirm Windows startup migration and no duplicate legacy instance after reboot.
- [ ] Verify sleep/wake, disconnected/offline source, source staleness, resume and display migration.
- [ ] Exercise safe uninstall/rollback to V5.1 while preserving settings and history.
- [ ] Identify the four Windows confirmation dialogs; do not disable Defender, SmartScreen or managed execution policies. The EXE remains unsigned.
- [ ] Compare displayed quotas to the **same ChatGPT account** official Codex dashboard at the **same instant**; match a real Codex Desktop primary turn duration to the observed journal.
- [ ] Obtain explicit approval to merge PR #14, create the final 5.2.0 installer/tag and publish a Release. CI green alone is insufficient.

## Test procedure on Windows

1. Use the latest green PR #14 artifact. Exit the previously running monitor completely before launching a new preview.
2. Verify the radar icons in the Start Menu, taskbar, tray and widget at normal scaling. Switch between Circular, Vertical, Compact and Micro layouts; ensure gauges and status text do not overlap.
3. Choose **Notifications → Test Windows notification**. Confirm whether a toast appears, with Windows Notifications enabled and Do not disturb disabled. The WinForms call alone is not proof of delivery.
4. Enable **Notifications → Enable notifications** AND **Codex task completed**. Run a Codex Desktop command and verify a single completion notification if the JSONL source actually exposes its terminal event; do not treat an unverifiable session as failure of Codex itself.
5. Reboot Windows and verify the configured startup preference works exactly once. Confirm the old SHINO startup entry does not spawn a second instance.
6. Verify history and colors remain, then compare quota values with the official dashboard. Keep notes/screenshots without secrets or session content.

## Known architectural limit

Codex local JSONL is undocumented, can be incomplete and cannot reliably identify the currently selected Desktop tab. An unobserved or stale activity must be labelled **UNVERIFIED**, never displayed as a current successful task. Windows notifications may be suppressed by system settings. Tests that use synthetic task sessions do not establish true end-to-end Desktop notification delivery.

Reference: [PR #14](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14) · [Roadmap](ROADMAP.md).
