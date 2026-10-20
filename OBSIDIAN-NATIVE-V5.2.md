# Obsidian Sonar -> native WinForms V5.2 candidate

PR #14 stays Draft. Stable Release and Premium Polish acceptance stay **HOLD** until the owner approves fresh screenshots and the remaining Windows field gates. This candidate does not install, merge, tag, publish a Release or change autostart.

## Reference and scope

The supplied Stitch ZIP contains one 443 x 1600 presentation board and its HTML. It is a visual reference, never an application runtime. The five mockups map to Circular, Vertical, Compact, Micro and blocked usage. No browser, server, remote fonts, CDN, image-generation dependency or paid model is used. The named screen recording was not included in the ZIP and was not located at the supplied/common download locations; no claim relies on inspecting it.

Initial integration baseline: `00d7820f8a19361ee210eb3e9dd16607634c38db`. The finish pass starts from the verified PR #14 head `f14a9a79bd5334f41da731336afc5491a22942a4`, green CI [39381459190](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/runs/39381459190); issue #15 remains open for owner acceptance. The two pre-existing V5.1 BAT edits remain outside this change.

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
| Circular | 540 x 398 | 98 px (73 px at the finish baseline); up to 104 px at wider preferences |
| Vertical | 355 x 383 | 78 px |
| Compact | 670 x 289 | 60 px |
| Micro | 484 x 230 | 40 px |

Circular still supports 470-720 logical pixels. Tight widths and multiple missions adapt the card to the available height; optional model/effort data remains in tooltips. The taller Stitch Micro geometry is deliberately not imposed.

## Obsidian finish from f14a9a7

- Circular redistributes its existing height: the activity panel grows from 100 to 130 logical pixels, while quota rings move from 130 to 118 pixels. Their readout stays about 32 pixels. The single activity radar is now 98 pixels, without enlarging the 540 x 398 window. Two missions keep separate radars and duration/state fields.
- Mission numbers precede project names so long names cannot hide the independent identity. Project/model/effort fields intentionally ellipsize with complete tooltips. Model and effort now use separate single-line controls instead of a wrapping two-line label. Critical states and durations stay visible, including the shorter rows in Vertical and Micro.
- Segoe UI readouts, resets and metadata have a clearer hierarchy and more contrast. Cached rounded inset faces, controlled top highlights, deeper lens tracks and thin semantic rails distinguish canvas, quota faces, source strip and activity cards. Existing custom colors, opacity and geometry remain preferences.
- A two-row source strip shows **CODEX USAGE BLOCKED**, **LAST KNOWN / CLI** and the actual observation timestamp. Unavailable, stale and unverified quotas have distinct headings. Local task states remain independent. No source time, completion, duration or progress is fabricated.
- Vertical, Compact and Micro gain aligned readouts, stronger metadata and inset tracks. Micro remains exactly 484 x 230; its strip/activity space is redistributed internally. Full-window review caught and corrected clipping of 100%, source-time height at minimum Circular width, and tight mission-label intersections.

## Full-window evidence

Finish captures are labelled **SYNTHETIC TEST DATA** (historical baseline captures say **SYNTHETIC FIXTURE**). Captures render the actual complete native window with controls at the requested scale, rather than enlarging a finished bitmap. Presentation-only windows have an explicit 4096 x 4096 maximum so a small CI virtual desktop cannot truncate a 200% native render; exact client dimensions are asserted. Production window constraints are unchanged. The fixture uses opacity 1 for inspectable screenshots; a separate assertion checks the custom opacity preference. This does not establish real DWM transparency or physical mixed-monitor behavior.

The original integration baseline is retained in `bin/instrument-qa/before`. The finish baseline was captured from f14a9a7 before the finish edits, with the same frozen fixture clock, and saved in `bin/finish-qa/before` with a source/hash receipt. The current 154 full-window captures cover four layouts at 100/125/150/200%, 0/1/2/3 turns (64), five degraded states at every layout/scale (80), two independent A/B transitions, and eight long-label/0%-100% cases. Required windows were inspected at 100% normal size, with additional 125/150/200% inspection; no physical DPI claim is made.

| Comparison | Before | After |
|---|---|---|
| Circular | [Full window](bin/finish-qa/before/Circular-1-100.png) | [Full window](bin/instrument-qa/after/Circular-1-100.png) |
| Vertical | [Full window](bin/finish-qa/before/Vertical-1-100.png) | [Full window](bin/instrument-qa/after/Vertical-1-100.png) |
| Compact | [Full window](bin/finish-qa/before/Compact-1-100.png) | [Full window](bin/instrument-qa/after/Compact-1-100.png) |
| Micro | [Full window](bin/finish-qa/before/Micro-1-100.png) | [Full window](bin/instrument-qa/after/Micro-1-100.png) |
| Blocked | [Full window](bin/finish-qa/before/Circular-Blocked-100.png) | [Full window](bin/instrument-qa/after/Circular-Blocked-100.png) |
| Two missions | [Full window](bin/finish-qa/before/Circular-2-100.png) | [Full window](bin/instrument-qa/after/Circular-2-100.png) |

[A completed / B active](bin/instrument-qa/after/A-completed-B-active-100.png). New captures and native reports are separate CI QA artifacts; synthetic fixtures are not added to the portable application ZIP. Before captures remain local evidence from the initial pass. See [native visual comparison](design-qa.md).

## Validation and cost

Local suites pass: 128 Python tests; C# 98 application + 46 task + 35 premium + 26 P0 + 100 full-window tests, **305 C# checks** in total. Full-window coverage includes all scales/counts, label/control bounds, critical text width, mission text intersections, reachability of overflow, A/B phase continuity, cached-data warnings, preserved preferences, repeated rescaling, motion gating and circular width limits. The finish adds 10 checks to the previous 90. Native Windows CI repeats the suites, icon/history checks and package/installer ValidateOnly validation. The exact final commit, CI run and artifact identities are recorded in the handoff/verification receipt after CI completes.

Fresh local comparison on the same machine, compiler/options and unchanged benchmark (12 logical processors, forced rendering of all quotas and three radars into a bitmap on each tick):

| Measurement | Recompiled f14a9a7 baseline | Finish |
|---|---:|---:|
| Visible frames / elapsed | 637 / 30.02 s | 636 / 30.03 s |
| Obtained FPS | 21.22 | 21.18 |
| CPU, total machine / one core | 1.449% / 17.38% | 1.379% / 16.54% |
| Hidden, five seconds | 0 frames / 0 clocks / 0.000% CPU | 0 frames / 0 clocks / 0.000% CPU |
| GDI, after 80 full-window cycles | 27 -> 28 | 27 -> 28 |
| USER, after 80 full-window cycles | 6 -> 6 | 6 -> 6 |
| Private memory, MiB | 31.76 -> 32.43 | 31.99 -> 32.45 |

The local comparison shows no significant rendering/resource regression; small differences are measurement noise, not a claimed speedup. Reports are retained in `bin/finish-qa/BASELINE_PERFORMANCE_REPORT.txt` and `FINISH_PERFORMANCE_REPORT.txt`. This is a deliberately heavier full-window capture workload, not an idle or field performance claim. CI runner measurements are reported separately rather than compared across different hardware.

## Remaining gates

- Owner screenshot approval for the new native composition, all four layouts and warning states.
- Actual physical 100-200% mixed-DPI display moves, real transparency, minimize/restore and sleep/wake observation.
- Two real concurrent Codex missions with terminal/notification checks and shell taskbar/Alt-Tab icon inspection.
- Remaining reboot, upgrade/rollback and notification-delivery gates in [RELEASE-GATE-V5.2.md](RELEASE-GATE-V5.2.md).

Synthetic HWND/scaling tests and green CI do not clear these field gates. Stable Release remains **HOLD**.
