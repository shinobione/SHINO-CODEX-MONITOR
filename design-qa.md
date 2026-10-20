# Final Target Match visual review — 2026-10-21

Current full-size reference/native viewer: `bin/final-target-match-20261020/COMPARAISON.html`. Five individual Stitch PNG/HTML references are unchanged. All native inputs and motion videos are explicitly synthetic. The old board and the previous df1779b comparisons are historical only.

| Screen | Corrected convergence | Residual / intentional difference |
|---|---|---|
| Circular | Twin gauge geometry, 03% with smaller percent sign, glow, accent-aligned REMAINING, structured mission metadata, one-line source/count/read time, common footer. | Native Consolas metrics and GDI+ blur differ; unsupported pin/minimize decoration is omitted. |
| Vertical | Source at top, stacked quota bars, central radar, centered title, metadata inset and elapsed row; two missions retain two centered scopes with independent states. | Two-task modules reduce each radar to 48 px; metadata remains visible at all tested scales. |
| Compact | Stable disjoint radar / quota / mission zones; caption/value hierarchy and bottom source/read/count strip. No quota reparenting or material churn during identical updates. | Global reset count lives in shared footer rather than being duplicated as a mission property; no fictional PID. |
| Micro | 484 x 230, compact quota/reset bands, mission strip, dedicated model/effort and time line; zero scroll with two missions. | Long metadata can ellipsize with full tooltip; two-task time prefix is `T`. |
| Blocked | Strong banner above quotas, red header/cached materials, real source time/count provenance and independently factual activity state. | Historical zero remains LAST KNOWN; no illustrative 429/retry/SUSPENDED. Unverified activity is identified rather than falsely paused. |

P0 diagnosis: the previous two-toggle model exposed legacy and target compositions inconsistently. Shared count and header fields varied between layouts. Compact detached and reparented quotas on each refresh; both Compact and Vertical alternated card colors. These paths are now separated into compatibility loading, explicit target selection, stable geometry and value updates. Authoritative data remains independent of presentation.

P1 comparison/correction loop: initial native captures exposed spacing errors, hidden Vertical metadata and overlong multi-task duration. Corrected label advances, z-order/background treatment, metadata grouping and compact duration copy; rerendered the complete target set. DPI/bounds tests caught one-pixel metadata intersections and narrow elapsed text; corrected placement rather than weakening assertions. Full before/after and raw optical references remain local.

Native screen-pixel clips for Vertical and Compact include one mission, arrival of B, and completion of A with B active. CSV records exact frame timestamps and observed parent/handle/material events. Zero blank probes and zero refresh churn are bounded evidence from those sampled clips, not a physical-display guarantee. Final owner approval and field validation remain HOLD.

P2 residuals: font substitution, native glyph metrics, exact CSS blur, decorative shell details and compact long strings. Existing geometry and color preferences are deliberately preserved until explicit layout selection. No independent reviewer/subagent was available; source/diff and frozen-core checks were reviewed inline. Current exact tests, performance and artifact identities are in `LIVRAISON.md`.

---

# Archived reviews — superseded candidates

# Current visual parity review - individual Stitch screens

**Internal synthetic review; owner visual sign-off HOLD.** Source project `7199182866908450683`, five unchanged individual PNG/HTML references. Earlier miniature-board reviews below are archived and are not the source of truth for this pass.

The local viewer `bin/parity-implementation/COMPARAISON.html` presents the five source windows and WinForms renders side by side at logical 100%, plus all four two-task variants and A-completed/B-active. The viewer preserves the raw reference PNGs. The application uses native controls and GDI+ only.

| Screen | P0 / factual outcome | P1 / implemented visual change | P2 / residual or intentional difference |
|---|---|---|---|
| Circular | Direct, stale, unverified and cached headings are distinct; only reliable raw zero is exhausted. | 680 x 480 opt-in, twin 144 px gauge controls, cached arc glows, resolved readout/caption accent, severity badges, 88 px single radar. | Windows fonts/kerning and percent typography differ; header tools and decorative footer are not copied. Source strip retains two lines for provenance. |
| Vertical | Source warning is independent of observed activity. | 360 x 580 opt-in, stacked quota bars and central 100 px radar with title/metadata below. | Metadata is inline rather than the source's nested subcard; secondary layout and blur/rasterization differ. |
| Compact | Source and per-turn states/durations remain separate. | 640 x 260 true three-zone dock: radar, stacked quotas, mission details; visible quota pixels are asserted. | Metadata uses separate readable rows; no fictional PID/reset-count or dock badge. |
| Micro | Cached reset is explicitly unverified; current absolute/relative reset fits. | Exactly 484 x 230, two quota columns, 46 px single radar, no scrolling with two missions. | Compact date format and secondary metadata reduction are intentional; full details remain in tooltips. |
| Blocked | Cached zero remains LAST KNOWN unless a reliable current raw zero is observed. Active local tasks stay active. | Red title/source material and warning triangle, muted cached rings/readouts, observation timestamp, distinct exhausted fixture. | Alert remains in the fixed source strip below quotas to avoid moving controls during a status transition. No fake HTTP 429, retry timer or SUSPENDED mission. This is a deliberate factual and geometric difference from the mock. |

Two missions are variants, not a sixth screen: Circular has two horizontal cards/radars; Vertical stacks two cards/radars; Compact splits the left radar and right information zones into two rows while retaining the center quota stack; Micro has two compact side-by-side cards. Teal and blue are independent; violet and amber extend the stable identity palette. Completion of A retains B's identity, accent, active state and clock. Optional long metadata ellipsizes with full tooltips; required state/duration/reset text is measured.

## Actual compare / correct / recapture loop

1. Retrieved individual Stitch references were matched against the initial native capture. Source tokens and exact window/radar geometry replaced the old board assumptions.
2. First opt-in capture exposed inverted PathGradientBrush falloff (hollow light rings), blank Compact quotas caused by overlapping HWND containers, and Micro reset truncation. Added failing halo/pixel/bounds checks reproduced these defects. The corrected native captures show filled light falloff and visible quota readouts.
3. Expanded 100-200% state-transition tests exposed the Blocked warning's shifted text bounds, 125% Compact row rounding and Micro's stale reset width. Corrected warning width, bounded row-local placement and compact reset copy; recaptured all windows.
4. Side-by-side 100% review refined the gauge calibration, face material, badge borders and blocked title/source material. Recovery is tested without moving quotas or stopping observed tasks. Additional 125/150/200% renders and all A/B variants are inspectable in the evidence set. Final small-width checks also reproduce and correct reset and state-label clipping at 470 px across all reset preferences; narrow cards shorten their state/duration copy while keeping full provenance in tooltips. The width menu now reports the active appearance mode width.

P0 functional gaps exercised here are corrected. P1 compositions and light layers are implemented and remain subject to owner approval; **no pixel-identical claim** is made. P2 residuals are native font metrics/letter spacing, exact CSS blur falloff, minor border/icon/decorative treatment and intentional data/provenance copy. Physical mixed-DPI and real observation/notification validation remain HOLD, not inferred from synthetic geometry.

The code was reviewed locally against frozen-core and package guards; no independent reviewer/subagent was available. CI repeats the full source/native/package checks. No field validation or release gate is marked complete by this review.

---

# Historical reviews (superseded reference; prior commits only)

# Native Obsidian visual comparison

Scope: adapt the supplied Stitch presentation to the existing native WinForms widget. The mission explicitly preserves preference geometry, Windows fonts and truthful source data; this is not a pixel-identical implementation of the tall HTML board. No browser workflow is used.

## Initial integration findings (00d7820 -> f14a9a7)

- **P1, baseline activity:** 24-30 px radars were dwarfed by quotas. Full-window baseline captures show the mismatch. Native single-mission radars are now 73/78/60/40 px at the four default geometries, with coherent adjacent title, status and elapsed text.
- **P1, baseline scrolling:** native AutoScroll produced phantom overflow. It is replaced by a bounded non-scrolling viewport and an explicit vertical track for real 3+ mission overflow. Full-window 0/1/2 mission bounds and scrollbar checks pass at 100-200% synthetic scales.
- **P2, first native iteration:** the remaining/last-known hint label overlapped the bottom of large percentages. Hint width now ends before the number. Fresh captures show complete digits in Vertical, Compact and Micro.
- **P2, hidden construction:** reading Control.Visible while the form was hidden allocated no timestamp width. Layout now uses explicit layout mode, so the source and observation time occupy their own bounds before first Show.
- **P2, minimum circular width:** the elapsed label exceeded the shorter 470-wide activity viewport. Its top is now bounded by actual card height; width-limit tests cover both 470 and 720 at all four scales.

- **P2, CI desktop constraint:** the runner capped wide 200% diagnostic windows at 1044 px. Presentation-only HWNDs now have an explicit maximum size and exact client-size assertions, including a deliberately reproduced smaller tracking limit. Native production constraints are unchanged; this is synthetic geometry proof only. Windows supports overriding its [default tracking dimensions](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-getminmaxinfo).

## Required fidelity surfaces

| Surface | Native comparison and decision |
|---|---|
| Typography | Segoe UI numerical hierarchy, bold quota values and compact uppercase instrument captions. Point sizes from the web mock are translated to owned native pixel fonts. Optional long task titles/model data may ellipsize with complete tooltips. Required controls and text heights are checked. |
| Spacing/layout | Nested quota faces, source strip and activity instrument follow the reference hierarchy. Existing geometry takes priority over the five suggested web sizes. Two narrow Vertical cards stack; Micro stays 484 x 230. |
| Colors | Obsidian inset surfaces, refined `#344B60` borders, brighter `#B3C4D5` secondary text and teal/amber/red semantic states. Existing custom quota bands/background/opacity remain preferences; Obsidian is opt-in. |
| Image/asset quality | Instruments are sharp native GDI+ geometry with cached backgrounds, not raster copies of the mock. This is explicitly required by the native mission. The embedded application icon remains separate from quota tray imagery. |
| Copy/data | Fictional stream cadence, latency, progress and reset counts from Stitch are omitted. Native copy distinguishes direct usage, last-known values and partial local task observation. All evidence fixtures are explicitly synthetic. |

Source: `bin/stitch-reference/screen.png` (443 x 1600), inspected with the full native windows. The native geometry/state/content intentionally differs from the tiny presentation examples, so no pixel-equivalence claim is made. Before/after evidence is indexed in [OBSIDIAN-NATIVE-V5.2.md](OBSIDIAN-NATIVE-V5.2.md); higher-scale native captures make text/bounds inspectable independently of the low-resolution board.

## Finish review (f14a9a7 -> current candidate)

The finish starts from the existing native Obsidian composition. Its before captures are frozen in `bin/finish-qa/before`, separate from the older integration baseline above. Complete windows were compared at their normal 100% size, including all four layouts, two Circular missions, blocked usage, unverified local activity and A-completed/B-active. Representative full windows at 125/150/200% were also inspected. All data is synthetic and marked in the capture footer.

| Layout/state | Visible finish and review result |
|---|---|
| Circular | Radar 73 -> 98 px, activity panel 100 -> 130 px, whole window still 540 x 398. Smaller quota lenses retain large percent values and readable resets. Numbered mission heading, state, duration and separated model/effort form a coherent instrument. |
| Vertical | Larger quota values and resets, a complete two-row source strip, stronger project/state/duration text and a clearly identifiable radar. Two tasks stack without unwanted native scrolling. |
| Compact | Horizontal quota balance preserved; inset tracks and source strip align with the activity card. Model and effort occupy separate rows without wrapping into each other. |
| Micro | Still 484 x 230, with larger quota digits and resets. Source time has its own row; project, state, duration and radar fit in the remaining activity space. Two tasks preserve their separate states and durations. Secondary long metadata is intentionally ellipsized or available in full tooltips. |
| Blocked / unverified | Red CODEX USAGE BLOCKED, neutral LAST KNOWN numbers and the real sample timestamp are distinct from amber quota/local UNVERIFIED states. Independently observed active tasks remain active under blocked quotas. |
| A completed / B active | A has its completion marker and recorded duration; B retains its teal sweep/state and elapsed duration. No collector/lifecycle changes were introduced. |

Corrections found during this visual/test loop: the old percentage box clipped 100%; the smallest Circular source strip clipped the taller timestamp font; tight mission labels intersected after typography changes; a multiline metadata label could hide effort behind a wrapped model. Dedicated value width, minimum source-strip height, bounded row heights and separate single-line metadata fields fix these issues. Mission numbers now precede long project names. Critical text width/height, sibling intersections and 0/100% values are checked in the 100 native tests, not inferred solely from control dimensions.

Fresh captures: 154 full-window PNGs across the four synthetic scales and relevant mission/source states. Internal fixture review found no remaining P1/P2 clipping or unnecessary scrollbar in the inspected windows. This does not assert owner acceptance or physical DPI behavior. Local resource comparison against recompiled f14a9a7 is recorded in [the implementation report](OBSIDIAN-NATIVE-V5.2.md#validation-and-cost).

## Gate

Internal native fixture review: corrected visible layout defects and required rendering checks pass after recapture. Owner visual acceptance: **PENDING / HOLD**. Physical mixed-DPI, real sessions, shell surfaces and system-delivered notifications remain unverified. No stable/premium completion claim is made.

final result: passed for the reviewed native synthetic fixture QA; owner sign-off remains HOLD.
