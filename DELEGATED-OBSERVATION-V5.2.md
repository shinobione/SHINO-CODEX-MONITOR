# Delegated Codex activity — V5.2 P0

## Root cause and changes

`LiveTaskReader.ReadCore` previously discarded every session marked `subagent` or carrying `parent_thread_id`. A cold read usually parsed only the final 512 KiB. Its 4 MiB fallback ran only when that tail yielded no turns, so an older terminal event could be missed when the session was much larger.

The reader now retains only the opaque `session_meta.payload.id` and `parent_thread_id` values needed for an exact join. A child is attributed only when its parent identifier exactly matches one root thread identifier and that root has exactly one open, identity-certain turn. Multiple open roots for that thread, missing IDs, duplicate child-session evidence and unlinked children are not joined. Root and child timestamps remain separate. Raw IDs are never included in diagnostics or presentation.

`DELEGATED · OBSERVED` is a separate mission/radar state. It animates from a fresh child observation while the parent has an open turn; it does not say the parent itself emitted a recent event. Stale child evidence returns to `UNVERIFIED`. A valid root terminal event wins over conflicting child activity. Only root terminals can produce notifications.

After process restart or cache loss, each cold source is streamed from the beginning with limits of 16 MiB per source, 1 MiB per line, 32 MiB per polling pass and 4 seconds for recovery work. Event records are parsed as the stream advances; message, reasoning and tool-output records are discarded. Incomplete recovery is marked uncertain and cached with its file length/write time, avoiding a repeated full scan while the source is unchanged. Appended files use the cached turn state plus the bounded tail; truncation/replacement forces a cold reconstruction.

The parser runs inside the existing background `Task.Run` reader. No quota collector, authentication, autostart, settings store or layout geometry changed.

## Verification

- 59/59 local Windows task fixtures pass, including exact metadata linkage, multiple roots/children, ambiguous/unlinked children, stale evidence, terminal conflicts, cache loss/restart, notification replay, malformed/missing/locked files and large JSONL recovery.
- The synthetic large-file fixture scanned 10,648,334 bytes in 99 ms locally; one session, zero recovery limits reached. This is a local synthetic I/O measurement, not a same-runner CPU/RAM benchmark.
- The full local V5.2 build also covers 98 application checks, 35 history/premium checks, 26 multi-radar checks, 123 instrument checks and 37 final target checks. Installer validation did not yield a fresh local ZIP when the running-instance guard blocked it; the CI build must provide the publishable Preview artifact.
- The CI performance workflow compares the current build against the exact preceding Preview run `39782385704`, sequentially on the same Windows runner. Results are synthetic and separate from local PC usage.

## Real-PC reproduction

1. On the Windows PC, start Periscope and begin a root Codex Desktop task that remains open while it starts a child task.
2. Keep the child active and produce a new child event within 90 seconds while the parent emits no new event. The parent's mission card should say `DELEGATED · OBSERVED`; its elapsed field is labelled `PARENT ELAPSED`.
3. Let child evidence age past 90 seconds. The mission must become `UNVERIFIED`, without continuing the delegated animation.
4. Complete the parent while a child is still active. The root card must show the confirmed parent terminal state and stop delegated animation. Completing a child alone must not send a root-completion notification.
5. Repeat with two children and with two distinct root threads. Confirm each child affects only its exact parent. Do not copy JSONL files or IDs into evidence; capture only the visible state and timing.

## Limits and remaining field checks

The local synthetic fixtures demonstrate the `id` / `parent_thread_id` join and large-file recovery. They do not prove that every installed Codex Desktop version emits these fields in the same rollout shape, nor prove live multi-parent Windows behavior. A real-PC reproduction remains required. Files above the configured byte/time budgets, oversized lines, malformed UTF-8, ambiguous identifiers and locked files intentionally remain unverified. The exact next manual action if the installer refuses validation is to close Periscope voluntarily, then rerun `BUILD-PERISCOPE-V5.2.ps1`; this work does not force-close it or bypass the guard.

