# P0 collector observability diagnostics

## Scope

This change only enriches the anonymized task diagnostic report. It does not change session discovery limits, activity freshness thresholds, parent-child correlation, ACTIVE/DELEGATED selection, notification transitions, or recovery completeness decisions. PR #18 remains untouched; Release remains HOLD.

## Added report fields

- Root session counts: recognized candidates, sessions examined under the existing 12-root cap, and candidates excluded by that cap.
- Child session counts: candidates, children linked by an exact observed parent identifier to one unambiguous root candidate, unlinked or ambiguous children, and active/fresh children.
- Selected global mission class: `ROOT ACTIVE`, `ROOT DELEGATED`, `UNVERIFIED`, `ROOT RECENT TERMINAL`, or `OTHER`, with a sanitized reason and the selected evidence age.
- Latest primary and child evidence ages, computed from the same `LastWriteUtc` observations already used by the collector.
- Aggregate reasons for bounded recoveries that were already limited: size/budget, timeout, invalid or oversized line, and partial read. Multiple reasons can apply to one session.
- Separate lists of up to six latest primary turns and six latest child turns. Source labels are allowlisted (`user`, `subagent`, `guardian_review`, `system`, or `other`).
- An explicit history note when the six newest turns are all terminal, explaining that terminal history does not itself determine current global status.

No raw session/thread identifiers, prompts, paths, arbitrary source strings, secrets, or JSONL lines are included. `guardian_review` is retained as a known source category because it is needed to explain the observed child sessions; all other source values are reduced to `other`.

## Supplied PC observation

The prior diagnostic stated 12 main sessions examined, 15 child sessions observed, four limited recoveries, global `ObservedActive`, and six latest completed turns with source `guardian_review`. That report did not contain the root candidate/exclusion split, verified child-link counts, selected mission identity class, proof ages, or recovery-limit reasons. Those values cannot be reconstructed from that older summary without rereading the live source; this change does not invent them. Copy a fresh report after installing/building this diagnostic update.

## Verification

- C# synthetic task suite: 63/63 passed. Added exact-cap checks with 12, 13 and 20 root candidates; linked and unlinked `guardian_review` children; distinct proof ages; recovery size/invalid-line/partial-read reasons; privacy redaction; and the case where six newest turns are completed while a separately evidenced root is active.
- Python static suite: 129 tests passed.
- Native C# compilation succeeded. Existing `MonitorForm.Menu` member-hiding compiler warnings remain unrelated.
- The oversized recovery fixture is bounded to the existing 16 MiB per-source ceiling and does not load the whole fixture into memory.

These are synthetic source and diagnostic checks. They do not modify or validate the user's current session state. No Codex session files were read for this report.
