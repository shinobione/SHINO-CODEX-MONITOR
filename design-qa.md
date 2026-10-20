# Native Obsidian visual comparison

Scope: adapt the supplied Stitch presentation to the existing native WinForms widget. The mission explicitly preserves preference geometry, Windows fonts and truthful source data; this is not a pixel-identical implementation of the tall HTML board. No browser workflow is used.

## Findings and corrections

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
| Colors | Obsidian inset surfaces, `#2A3C50` borders and teal/amber/red semantic states. Existing custom quota bands/background/opacity remain preferences; Obsidian is opt-in. |
| Image/asset quality | Instruments are sharp native GDI+ geometry with cached backgrounds, not raster copies of the mock. This is explicitly required by the native mission. The embedded application icon remains separate from quota tray imagery. |
| Copy/data | Fictional stream cadence, latency, progress and reset counts from Stitch are omitted. Native copy distinguishes direct usage, last-known values and partial local task observation. All evidence fixtures are explicitly synthetic. |

Source: `bin/stitch-reference/screen.png` (443 x 1600), inspected with the full native windows. The native geometry/state/content intentionally differs from the tiny presentation examples, so no pixel-equivalence claim is made. Before/after evidence is indexed in [OBSIDIAN-NATIVE-V5.2.md](OBSIDIAN-NATIVE-V5.2.md); higher-scale native captures make text/bounds inspectable independently of the low-resolution board.

## Gate

Internal native fixture review: corrected visible layout defects and required rendering checks pass after recapture. Owner visual acceptance: **PENDING / HOLD**. Physical mixed-DPI, real sessions, shell surfaces and system-delivered notifications remain unverified. No stable/premium completion claim is made.

final result: passed for native synthetic fixture QA; owner sign-off remains HOLD.
