# Obsidian Sonar -> native WinForms V5.2 candidate

PR #14 stays Draft. Stable Release and Premium Polish acceptance stay **HOLD** until the owner approves fresh screenshots and the remaining Windows field gates. This candidate does not install, merge, tag, publish a Release or change autostart.

## Reference and scope

The supplied Stitch ZIP contains one 443 x 1600 presentation board and its HTML. It is a visual reference, never an application runtime. The five mockups map to Circular, Vertical, Compact, Micro and blocked usage. No browser, server, remote fonts, CDN, image-generation dependency or paid model is used. The named screen recording was not included in the ZIP and was not located at the supplied/common download locations; no claim relies on inspecting it.

Baseline: `00d7820f8a19361ee210eb3e9dd16607634c38db`, green PR #14, issue #15 open. Only the two pre-existing V5.1 BAT edits were present before this slice; they remain outside this change.

Execution order: build isolated full-window capture first; capture the unchanged composition; add failing visible-radar/scroll tests; implement native instrument surfaces and responsive mission cards; inspect full windows and fix clipping; validate scaling, independent transitions, disposal and performance; run Windows CI and verify the seven-file package. Owner approval is a final gate on the resulting screenshots.

## Native implementation

- Cached inset panels, bezel rings, restrained one-pixel borders, Segoe UI labels and larger numerical values replace the flat composition. GDI+ static bitmaps and owned fonts are disposed.
- Existing background, opacity, quota thresholds/colors, circular width and placement remain preferences. **Appearance -> Background color -> Obsidian / Sonar** is an optional `#080D14` preset; no existing theme is overwritten.
- One mission receives the prominent instrument. Two are balanced cards; Vertical stacks them within its existing height. Three or more use a condensed grid/list and an explicit narrow vertical overflow track. The viewport has `AutoScroll=false`, zero scroll margin/minimum size, bounded widths and guarded layout reentrancy. No horizontal scrollbar exists; 0-2 cards fit without scrolling.
- One shared production motion timer/clock, independent phase offsets and existing observed completion deduplication remain. Hidden, minimized and suspended rendering stops. Task activity never animates the tray icon continuously.
- Blocked/cached quotas say **LAST KNOWN**, with their actual observation time. A fresh explicit reset remains available when ordinary usage is blocked. Blocked usage does not mute independently observed JSONL tasks. Missing/unverified/stale observations do not invent completion or progress.
- `MonitorForm(Preferences)` is a presentation-only construction path using the production controls. It creates no LocalStore, tray, readers, system-event subscription or persistence. Diagnostic flags run before the normal instance mutex, so testing does not require closing the installed widget.
- The collector, turn identity rules, authentication, persistence, history and alert core are unchanged and covered by normalized source hashes in `tests/obsidian-core-sha256.json`.

| Layout | Preserved default logical window | Single radar control |
|---|---:|---:|
| Circular | 540 x 398 | 73 px; up to 96 px at 720-wide preference |
| Vertical | 355 x 383 | 78 px |
| Compact | 670 x 289 | 60 px |
| Micro | 484 x 230 | 40 px |

Circular still supports 470-720 logical pixels. Tight widths and multiple missions adapt the card to the available height; optional model/effort data remains in tooltips. The taller Stitch Micro geometry is deliberately not imposed.

## Full-window evidence

All inputs are labelled **SYNTHETIC FIXTURE**. Captures render the actual complete native window with controls at the requested scale, rather than enlarging a finished bitmap. The fixture uses opacity 1 for inspectable screenshots; a separate assertion checks the custom opacity preference. This does not establish real DWM transparency or physical mixed-monitor behavior.

Before captures were made first from the baseline composition plus isolated presentation wiring and a frozen fixture clock. They cover all four layouts with 1/2/3 turns and Circular blocked usage at 100%. After captures cover all four layouts at 100/125/150/200%, 0/1/2/3 turns, blocked/offline/stale states, A completed while B stays active, and blocked usage with two independently observed tasks.

| Comparison | Before | After |
|---|---|---|
| Circular | [Full window](bin/instrument-qa/before/Circular-1-100.png) | [Full window](bin/instrument-qa/after/Circular-1-100.png) |
| Vertical | [Full window](bin/instrument-qa/before/Vertical-1-100.png) | [Full window](bin/instrument-qa/after/Vertical-1-100.png) |
| Compact | [Full window](bin/instrument-qa/before/Compact-1-100.png) | [Full window](bin/instrument-qa/after/Compact-1-100.png) |
| Micro | [Full window](bin/instrument-qa/before/Micro-1-100.png) | [Full window](bin/instrument-qa/after/Micro-1-100.png) |
| Blocked | [Full window](bin/instrument-qa/before/Blocked-100.png) | [Full window](bin/instrument-qa/after/Circular-Blocked-100.png) |
| Two missions | [Full window](bin/instrument-qa/before/Circular-2-100.png) | [Full window](bin/instrument-qa/after/Circular-2-100.png) |

[A completed / B active](bin/instrument-qa/after/A-completed-B-active-100.png). New captures and native reports are separate CI QA artifacts; synthetic fixtures are not added to the portable application ZIP. Before captures remain local evidence from the initial pass. See [native visual comparison](design-qa.md).

## Validation and cost

Local suites: 128 Python tests; existing C# 98 application + 46 task + 35 premium + 26 P0 tests. 89 new full-window tests cover all scales/counts, label/control bounds, reachability of overflow, A/B phase continuity, cached-data warnings, preserved preferences, repeated rescaling, motion gating and circular width limits. Native Windows CI repeats the suites and package validation. The exact final commit, CI run and artifact identities are recorded in the handoff/verification receipt after CI completes.

Local full-window benchmark (12 logical processors, forced rendering of all quotas and three radars into a bitmap on each tick): 635 frames / 30.01 s, 21.16 FPS, 1.484% total-machine CPU / 17.80% of one core. Hidden for five seconds: zero frames, zero active radar clocks, measured 0.000% total-machine CPU. Across 80 whole-window create/scale/render/dispose cycles: GDI 28 -> 28, USER 6 -> 6, private memory 31.81 -> 32.49 MiB. This is a deliberately heavier full-window capture workload, not an idle or field performance claim.

## Remaining gates

- Owner screenshot approval for the new native composition, all four layouts and warning states.
- Actual physical 100-200% mixed-DPI display moves, real transparency, minimize/restore and sleep/wake observation.
- Two real concurrent Codex missions with terminal/notification checks and shell taskbar/Alt-Tab icon inspection.
- Remaining reboot, upgrade/rollback and notification-delivery gates in [RELEASE-GATE-V5.2.md](RELEASE-GATE-V5.2.md).

Synthetic HWND/scaling tests and green CI do not clear these field gates. Stable Release remains **HOLD**.
