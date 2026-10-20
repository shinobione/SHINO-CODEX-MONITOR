# CODEX PERISCOPE V5.2 — Stable release preparation (unpublished)

## Scope and baseline

Prepared from the existing Draft PR #14 candidate \`074b41fddbd52b3c339d5b3d39ce89b2ebc2c86f\`, independently verified with owner-reported seven real Windows field PASS items. **This branch does not alter the quota/task collector, the WinForms design, user settings, startup, or installed application.**

This is release-candidate engineering only. Parent PR #14 preserves the field-validated \`5.2.0-preview.1\` code. This dependent branch declares stable \`5.2.0\` solely to validate the as-yet-unpublished final ZIP; no merge or publication is authorized.

## Files added

- \`BUILD-RELEASE-V5.2.ps1\`: dedicated stable 5.2.0 build. Refuses the preview assembly informational version, runs the native tests and installer validation, packages exactly the updater-expected \`CODEX_PERISCOPE_V5_2_WINDOWS.zip\` with manifest, and never installs/publishes.
- \`README-EN-STABLE-V5.2.txt\`: stable README staged as \`README-EN.txt\` only for stable builds. The current preview's README is not overwritten.
- \`RELEASE-NOTES-V5.2.md\`: proposed public notes with data-source, unsigned binary, rollback and source transparency.
- \`.github/workflows/publish-v5.2.yml\`: manual-only workflow. Restricts execution to \`main\` with stable \`AssemblyInformationalVersion("5.2.0")\`. Default \`publish: false\` builds and uploads an unpublished stable candidate; \`publish: true\` is a separately authorized operator action. The publication path rejects an existing tag/Release, creates a draft, downloads and compares exact ZIP bytes, then makes the verified draft public. No automatic publication occurs on push, tag or PR.
- \`tests/test_release_engineering_v52.py\`: checks the allowed release identity, distinct archive names, manifest/README boundaries and manual-only gate.
- \`.github/workflows/build-windows.yml\`: chooses Preview or stable build by explicit assembly informational version; uploads separately named unpublished artifacts; never publishes.
- \`RELEASE-GATE-V5.2.md\`: records seven user field PASS items, separate from CI and publication approval.

## Explicit unresolved work BEFORE stable promotion

1. Integrate this dependent PR after a green PR/branch CI result, without merging PR #14 to \`main\` prematurely.
2. **Candidate prepared:** this dependent branch now declares \`AssemblyInformationalVersion("5.2.0")\`; parent PR #14 retains Preview. Do not publish this candidate until stable CI on the final source tree is inspected and the user authorizes publication.
3. Update \`README.md\`, \`CHANGELOG.md\`, \`ROADMAP.md\`, the relevant old preview status paragraphs of \`RELEASE-GATE-V5.2.md\` and issue #15 to reflect owner visual sign-off and release-readiness, **only once verified**.
4. Execute \`BUILD-RELEASE-V5.2.ps1\` on Windows with the stable identity, verify all Python and C# suites, installer \`-ValidateOnly\`, ZIP entry allowlist, ZIP and executable SHA-256. Do not assert a Windows stable build passed before the workflow runs.
5. Re-run Windows CI against the actual final source commit and confirm the unpublished stable ZIP matches the \`src/ReleasePackage.cs\` and \`src/ReleaseUpdater.cs\` download contract.
6. The four historical Windows confirmation dialogs are not precisely classified. Keep the unsigned warning transparent and never bypass Defender, SmartScreen or managed restrictions.
7. Request the owner's **separate explicit authorization** to merge PR #14, create tag \`v5.2.0\` and publish the public GitHub Release.
8. After approval and merge, run the manual publication workflow on \`main\` with \`publish: true\`. It creates a draft and checks downloaded bytes before going public; verify the published latest-release API metadata and a real updater read-only download. If GitHub digest/API or CDN checks fail, report HOLD and leave manual recovery to the maintainer; do not replace an existing tag silently.

## Evidence boundaries

Owner field validation is a human test report, not a direct CI measurement. The 128 Python + 305 C# totals and verified Preview package refer to the previous \`074b41f\` candidate. Any source change, including this release-engineering work, must have its own CI result before describing it as tested.

- Product only monitors Codex quotas attached to a ChatGPT account, not general ChatGPT or API billing.
- Local task JSONL source is undocumented; no live/complete status may be inferred without evidence.
- No passwords, cookies or tokens may be copied into logs, source or artifacts.
- Existing directory names and executable filename remain for V5.1 backward compatibility.

**Current decision: stable release HOLD.** Draft PRs and manual package prep only. No live binary, tag or publication has been created by this work.
