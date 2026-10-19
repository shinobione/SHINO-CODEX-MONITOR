# Security and Privacy

**Codex Periscope** is an independent, local Windows app for Codex quota monitoring and read-only observation of Codex activity. It is not affiliated with OpenAI.

## Authentication and privacy
- Quotas are read from the installed, already authenticated Codex CLI using `account/rateLimits/read`; the widget never asks for your ChatGPT password.
- Never extract `.codex/auth.json`, cookies, browser tokens, private API endpoints or raw conversation prompts.
- Activity relies on **undocumented local JSONL files**; it may not match the current Codex Desktop tab.
- Settings and history remain local in the original `%LOCALAPPDATA%\SHINO-CODEX-MONITOR` folder for safe upgrade.
- No paid AI inference, external telemetry endpoint, hosted server or recurring fee.

## Repository hygiene
Public commits must contain only source code, icons, build scripts, tests, and non-sensitive documentation. Never commit `settings.json`, `history.jsonl`, `.codex` data, cookies, tokens, private screenshots, diagnostic logs, `.env` or credentials. Run `git status`, inspect staged files and execute the test suite before proposing a PR. Gitignore does not protect files that are already tracked.

## Responsible disclosure
Do not post vulnerabilities or sensitive values in public issues. Contact the maintainer privately or use GitHub private vulnerability reporting if available. Revoke any exposed credentials; deleting a commit does not invalidate leaked secrets.

## Windows package integrity
SHA-256 verifies bytes, **not publisher identity**. Build artifacts are unsigned and may trigger normal Windows protection prompts. Never disable Defender, SmartScreen or organization security policies to run the preview.

The V5.2 installer must verify the binary version, SHA-256 and embedded tests **before any installation changes**. Migration of Windows startup entries requires explicit user consent and must not remove unrelated entries.
