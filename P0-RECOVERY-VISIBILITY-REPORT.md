# P0 recovery starvation and mission visibility

Status: **implementation complete locally; remote CI and field validation pending; Release HOLD**.

## Confirmed causes

- The collector limited a cold recovery to a 16 MiB prefix. An incomplete scan was cached against the file's current size and timestamp, then returned unchanged on later polls. When it did rescan, it started at byte zero. A start event around 18.88 MiB in a 22.13 MiB session therefore remained outside the parsed prefix.
- `MissionTracker.At()` returned cards by historical mission number. `MissionDeck` used the same order, so old cards could consume the visible rows before a new ACTIVE or DELEGATED mission.

These findings explain collection loss and a separate presentation-priority defect. They do not establish that a Codex Desktop tab is ACTIVE without source evidence.

## Changes

- Recovery uses a verified Windows file identity (volume serial and file index), committed line-boundary offsets, 2 MiB slices, a 32 MiB per-poll budget, a 64 MiB per-source ceiling, a 4 second read deadline and a 1 MiB line limit. Sources rotate the first recovery slot between polls. The stable freshest-root admission set remains capped at 12 regardless of that rotation.
- A slice ending inside a UTF-8 record resumes from the prior complete line, with bounded overlap and no raw line fragment retained. Oversized lines are skipped through their next newline. Truncation or replacement invalidates the checkpoint and header cache. If file identity cannot be verified, recovery does not repeatedly reread the prefix.
- Cached turn metadata remains canonical. Every turn from an incomplete source is projected as uncertain until recovery completes; partial terminal events cannot confirm completion. A terminal first exposed after an uncertain partial observation is suppressed from notifications as historical recovery.
- Mission cards sort ACTIVE, DELEGATED, UNVERIFIED/STALE, then terminal, with stable mission numbers as the tie-breaker. Priority changes reset scrolling to the first complete row. The viewport shows only complete cards and advances by row-sized steps. Existing card identity and accent remain keyed to the historical mission number.
- Diagnostics show bytes read this poll and aggregate checkpoint progress. They do not include paths, session identifiers, prompts or JSONL text.

## Privacy and resource boundaries

The scanner remains local and read-only. It does not call a model or read authentication material. File identity and offsets are process-memory metadata only; JSONL content is not persisted. Raw session IDs remain confined to relationship matching in memory and are excluded from reports. Confirmed ACTIVE/DELEGATED rules are unchanged. Partial recovery cannot confirm ACTIVE or terminal state.

## Local verification

- Windows .NET Framework compiler: successful full-source compile. Two existing `MonitorForm.Menu` member-hiding warnings remain.
- C# self-tests: 98/98 passed.
- Task fixtures: 69/69 passed, including the 18.88 MiB event, two growing sources, UTF-8 slice boundary, replacement/truncation invalidation, notification suppression and recovery cap.
- P0 multi-window/layout tests: 27/27 passed, including all eight Classic/Obsidian layout combinations with ACTIVE, DELEGATED and two stale missions.
- Instrument tests, premium tests and target tests passed in the local Windows run.
- Python suite: 129 tests passed.
- Synthetic recovery measurement: a 10,650,320-byte fixture completed in 6 calls and 72 ms of summed scanner time. This is a fixture measurement, not a comparison against the prior candidate or a field-device benchmark.

## Not yet verified

- No CI has run on a new commit. The local Git object for PR #18's requested start point is absent and the sandbox does not permit Git writes. The existing PR #19 branch is the intended parent; a new Draft PR must be stacked on it to remain transitively based on #18.
- No preview ZIP has been generated locally. `BUILD-PERISCOPE-V5.2.ps1` runs the installer `-ValidateOnly` gate, and that gate refuses to proceed while Periscope is open. The running instance was not closed or bypassed. CI can execute the gate in its clean runner and produce the ZIP artifact if the workflow publishes it.
- The layout captures are synthetic native-control renders. No real-PC 100% DPI capture set or anti-blinking video was produced. No three-mission field reproduction was performed.
- No before/after CPU, RAM, FPS, GDI or USER comparison was available under identical conditions.

## Field procedure

1. Close Periscope voluntarily from its tray menu before running the installer validation. Do not force-terminate it.
2. Build and install the CI-validated Preview manually. Record Windows display scaling at 100% and the exact Preview SHA-256.
3. Start three independent Codex Desktop missions, including one delegated task. Keep the app open and confirm the local JSONL roots and verified parent identifiers before interpreting cards.
4. Capture all eight layouts at native 100% DPI, then record repeated Vertical and Horizontal Dock refreshes plus ACTIVE→COMPLETED and DELEGATED→UNVERIFIED transitions.
5. Capture CPU, working set, FPS, GDI and USER for this candidate and the preceding V5.2 candidate with the same machine, workload, display and sampling interval.

No merge, tag, installation, autostart change or Release publication was performed.

