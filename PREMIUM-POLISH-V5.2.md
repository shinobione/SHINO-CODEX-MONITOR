# CODEX PERISCOPE 5.2 Preview — premium candidate

Status: **HOLD for stable release**. Native Windows Preview; no installation, tag, Release, merge or startup change authorized or performed by this work.

## Scope and implementation record

The supplied Premium Polish mission is the design brief. Execution order: baseline audit; history normalization and plotting; radar rendering; native UI refinement; synthetic Windows verification; Preview packaging and Windows CI. Continue the existing Draft [PR #14](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14) from baseline `4346160668959d3f6b0c76aeb36558d424dab101`.

- `HistoryData.cs`: read-only loading, validation, UTC normalization, per-series conflict handling, filtering, statistics and line continuity. `HistoryWindow.cs`: native chart, controls, keyboard-readable data table, tooltips and summary cards. The renderer never edits observations.
- `ActivityGlyph.cs`: independent evidence-to-render mapping; cached GDI+ lens/rings; 48-degree sweep, deterministic contact/pulse, and 900 ms confirmed completion transition. Rotation uses `Stopwatch`, one revolution per three seconds. No activity is inferred from rendering.
- `MonitorForm.cs`: requests at most 29.4 frames/s (34 ms timer), stops graphical timing while hidden, suspended or disabled, and forwards confirmed terminal transitions through existing rules. Notification gates, source cadence and single-instance identity are retained. No startup completion replay.
- Premium / Deep Instrument is an optional dark-background preset. Existing quota threshold colors, stored position, opacity, dimensions and other preferences are preserved. Gauges explicitly label REMAINING; they do not animate unmeasured changes.
- Existing English presentation corrected for long task durations and date tooltips. Collector/parser behavior is unchanged.

## Quota History V2 guide

Choose **History → Show history**. Time filters: **24H / 3D / 7D / 30D / ALL**. Series: **5H / WEEKLY / BOTH**. Choices persist across window reopening within the app session; no preference migration is needed. **Data table** exposes exact recorded values, source and reset information using native keyboard navigation.

X positions use UTC instants; labels and tooltips use Windows local time and English dates. The Y domain is always 0–100% remaining, with 25-point ticks. Each summary column reports real selected observations independently for 5H and weekly. SAMPLES in the summary counts valid series points; the header counts records containing at least one valid series.

Straight segments connect compatible observations at most **30 minutes** apart. A missing/invalid series value breaks that series. A rise, changed reset window or changed source breaks the line. Corruption without a usable timestamp disables connecting segments conservatively because the missing interval cannot be located. Conflicting duplicate values cannot be resurrected by later duplicates. Invalid rows are reported and never converted to zero.

An **observed reset** requires two compatible observations no more than 30 minutes apart: remaining quota increased, the previously recorded reset lies between observations, and the next reset advanced beyond the confirming observation. The marker is at the first confirming sample, not an invented exact reset time. Its tooltip explains the evidence. Passing a predicted reset time alone never confirms a reset. Dashed expected-reset guides use recorded metadata only and are shown only inside the plotted period. Unexplained replenishments remain disconnected and unconfirmed.

Legacy V5.1 rows (`utc`, `five`, `week`) remain readable without rewriting their bytes. Their source is labelled **Not recorded (legacy/local history)**; missing reset metadata stays missing. Future appended rows additionally retain source and expected reset timestamps. The previous 14-day/4,200-row destructive pruning is removed: ALL means all retained records. Previously deleted historical records cannot be recovered. The file can grow over time; no database, cleanup or new retention deletion is introduced.

RECORDED means a stored observation, never live task activity. The header labels its latest stored record STALE after **10 minutes**, reflecting the existing five-minute recording interval. A 30-second visible-window timer refreshes this age; it stops when hidden. Historical points remain valid historical observations as they age; explicitly stale/invalid source rows are excluded. Quota freshness and task evidence remain separate.

## Verification and non-regression

Baseline on this Windows host: **113 Python audits, 98 C# application tests, 46 C# activity tests** passed; the earlier mission figure of 109 Python audits was older than this checkout. Candidate: **118 Python audits** (five added architecture guards), **98 application**, **46 activity**, and **35 new native premium checks** passed locally. The original C# test counts are not reduced. New checks are required by the Preview build and therefore by Windows CI.

Coverage includes empty/single/irregular samples, independent/absent series, sorting, exact/conflicting duplicates, null/invalid/boundary percentages, explicit stale rows, locked source, gaps, expected/unconfirmed/observed resets, DST repeated times, mixed offsets, offset-less/future rejection, 30D/ALL legacy reads with byte preservation, statistics, real filter clicks, keyboard table, window resizing, 3,462-point drawing, radar evidence mapping, completion deduplication, startup/hidden/disabled/resume behavior, and repeated resize/theme drawing. Fixture files remain isolated in build output; no synthetic samples enter the user's history.

The actual ICO frames are decoded and pixel-checked at 16/32/48/256 px. Installer testing uses **-ValidateOnly**; it verifies EXE SHA-256 plus application/activity tests before returning without installation. The build additionally validates the final ZIP's seven-file allowlist and its embedded EXE SHA-256 against the tested image.

Local final-package limitation: the last two local build attempts reached the installer validation and then displayed Windows **Open File - Security Warning** dialogs. Only those two mission-owned PowerShell helpers were canceled; no warning was accepted, no protection was bypassed, and the already installed Periscope process was left running. The initial baseline and an earlier intermediate build completed `-ValidateOnly`, but those do **not** certify the final candidate package. The final installer/ZIP gate must pass on the isolated Windows CI runner. Native candidate C# tests and ICO pixel checks completed locally before the dialogs.

Two pre-existing modified V5.1 batch launchers are deliberately excluded from this change. Three existing compiler warnings remain: two `Menu` hiding warnings and unused `Meter.Data`.

## Performance observations

Local reference environment: Windows NT 10.0.26300, native x64 .NET Framework / GDI+, 12 logical processors, AMD64 Family 25 Model 33 Stepping 2. No hardware serial, user data, paths or session content is included in exported reports.

Synthetic component benchmark (no live collectors): **633 frames in 30.11 seconds**, **21.02 FPS**, **0.069% total-machine CPU** / **0.83% of one core**, longest frame interval **63 ms**. This meets the measured CPU target but is below a locked 30 FPS; Windows Forms timer scheduling is not a precise frame clock. The angle still advances from monotonic elapsed time. Hidden for five seconds: graphical timer disabled, **zero extra frames**, **0.000% CPU** at measurement resolution; a non-graphical sampler remains in the benchmark.

Chart: **3,462 real fixture points**, approximately **14–16 ms/frame** across local DrawToBitmap runs. Radar 100 resize/theme cycles: GDI handle count unchanged (latest run **40 → 40**). After five warmup History shows, three batches of 25 show/close cycles: private bytes **31,469,568 → 31,834,112**, **31,772,672 → 31,883,264**, **31,883,264 → 31,899,648**; GDI **16 → 16** each batch. Initial cold allocation grew substantially; the warmed batches settle rather than showing sustained handle growth. These are finite native fixture measurements, not a long-running leak proof or installed-widget performance certification.

Fresh machine-readable results live in `PREMIUM_TEST_REPORT.txt` and `PREMIUM_PERFORMANCE_REPORT.txt` inside the separate CI QA artifact. CI runner values may differ from the local observations above.

## Evidence and remaining Windows field checks

All generated PNGs are labelled **SYNTHETIC TEST DATA**. QA output includes ACTIVE/COMPLETED/UNVERIFIED radar rendering; actual radar sizes used by all four layouts; 24H and 7D charts with a gap; observed-reset chart; and History layout scaling at 100/125/150/200%. Images exercise native controls. Scaled layouts are **not physical DPI tests**. Radar-size images are **not full-widget screenshots**. Full widget composition in all four layouts still needs field screenshots.

The mission supplies five previously obtained user field confirmations: smooth radar in four layouts, Windows notification delivery, one notification per completed task, one instance after restart, and preservation of colors/position/history. They remain baseline owner evidence; this agent did not repeat those checks or downgrade them into synthetic evidence. Reconfirm them on this candidate before stable promotion.

Remaining: installed-widget visual smoothness/legibility; full Circular/Vertical/Compact/Micro screenshots; actual 100/125/150/200% DPI and mixed-DPI monitor movement; sleep/wake without replay; notifications enabled/disabled and duplicate suppression after restart; single instance after reboot; source unavailability and freshness; existing custom palette/position/history preservation; optional rollback/uninstall with data preservation. No stable GO follows from CI alone.

## Preview artifacts

- Existing Draft PR: [#14](https://github.com/shinobione/SHINO-CODEX-MONITOR/pull/14).
- Windows build workflow: [Build Windows](https://github.com/shinobione/SHINO-CODEX-MONITOR/actions/workflows/build-windows.yml). Select the run matching the premium candidate commit.
- App artifact: **CODEX-PERISCOPE-V5.2-PREVIEW-WINDOWS**, containing `CODEX_PERISCOPE_V5_2_PREVIEW_WINDOWS.zip`.
- Separate evidence artifact: **CODEX-PERISCOPE-V5.2-SYNTHETIC-QA**, containing labelled PNGs and native reports.
- Version remains **5.2.0-preview.1 / 5.2 Preview**. Git commit plus artifact SHA-256 identifies this candidate. The binary is unsigned. No public Release exists for this candidate.

**Release recommendation: HOLD.** Preview candidate for review and field verification only.
