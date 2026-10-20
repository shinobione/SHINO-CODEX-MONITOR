# CODEX PERISCOPE — V5.2 Preview

**A native Windows quota and activity radar for Codex.**

Codex Periscope is an **independent community application, not affiliated with OpenAI**.

## Product scope
- Remaining ChatGPT-account Codex quotas and reset times, only when exposed by the Codex CLI.
- Read-only observation of local Codex turns and their model, effort and elapsed duration.
- No generic AI providers, API billing dashboard, cloud server or paid AI calls.
- Never present old or missing data as live.

## Design
- Radar/periscope lens icon with cyan sweep and target blip.
- Deep navy background with subtle concentric rings.
- Quota colors remain explicit: violet, blue/teal, yellow, orange, red.
- A sweep runs only for observed activity; a green check indicates a verified terminal event.
- `UNVERIFIED` means the source is not sufficient; it does **not** mean Codex is idle.

## Language
**All user-facing surfaces are English only**: UI, menus, dialogs, tooltips, installer/uninstaller, notifications, error/status text, documentation and update prompts. Internal legacy paths may retain their historical names for compatibility and must not become UI labels.

## Migration safety
- Upgrade in place from V5.1 without wiping local settings or quota history.
- Preserve existing startup registry value until a safe, verified migration is available.
- Avoid launching duplicate widgets on startup.
- Keep the existing GitHub repository and update channel through the transition.
- Do not publish, rename the repository or alter a user's installation automatically.

## Release gate
The 5.2 Preview is **not** a stable release. Require a green Windows CI run and a real Windows migration test from 5.1, including startup, tray, update, rollback, DPI and offline behavior before approval for publication.

[Project roadmap](ROADMAP.md) · [Development issue](https://github.com/shinobione/SHINO-CODEX-MONITOR/issues/12)
