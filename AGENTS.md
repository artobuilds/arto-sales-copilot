# Arto Sales Copilot

Native Windows WPF (.NET 10) call assistant. Keep changes scoped and preserve user data.

- UI languages RU/UK/EN are independent of conversation language. Day/Night themes use semantic dynamic resources. Keep the voice strip visible at 1000x720.
- Jev selects coaching moves and verifies optional OpenAI wording. Do not bypass factual/relevance checks or silently replace selected providers/models.
- No startup capture, meeting bots, uploads, notifications or background services. Explicit Start/Stop; loopback records all applications on the selected output.
- Voice observations are local YIN/RMS/VAD/pace comparisons, not emotion recognition, lie detection or automatic speaker identification. Preserve baseline/quality/epoch gates.
- Never commit credentials, settings, personal/client briefs, transcripts, recordings, provider responses, model weights or runtime environments. A .gitignore is only a first layer: inspect staged files before pushing.
- Do not add developer-specific absolute paths. Keys use DPAPI CurrentUser; imports read only a user-selected file, with no secret values in logs.
- Use PowerShell on Windows and apply_patch for manual edits. Do not modify shared Python runtimes or install global software as a build side effect.
- Required code checks: Release build, --self-test and --ui-smoke. Use only synthetic content; real audio and paid provider checks require separate explicit authorization.
- Keep README, PRIVACY and THIRD-PARTY-NOTICES accurate. Offline checks do not prove live-provider availability or actual Zoom recording quality.
