# Development notes

This preview is developed with Windows, PowerShell, .NET 10 and WPF. Keep the native desktop architecture and the RU/UK/EN translation catalog aligned.

For a proposed change, open an issue with a fictional reproduction, or fork the repository and submit a pull request. The owner reviews proposed changes before accepting them. Do not include client text, API keys, recordings, provider account identifiers or copied private diagnostics. Submit original code or code you are authorized to contribute under the project's MIT license; preserve third-party notices.

Before submitting a code change:

1. Build the current sources with `build.ps1`.
2. Run `--self-test` and `--ui-smoke` on the newly published executable; inspect results under the ignored `test-output/` directory. UI checks need an interactive Windows desktop.
3. For changes to acoustic code, run `prosody_tests.py` with the optional Python runtime.
4. Inspect `git diff --cached` and `git ls-files`. Never stage local-data, recordings, credentials, logs or model files. `.gitignore` does not remove files that were previously tracked. When Claude Code works in this repository, `.claude/skills/arto-commit-guard` also refuses its commits and pushes that contain such files; it is an extra layer, not a substitute for this inspection.
5. State what was tested and what was not. Live capture, client calls and paid provider requests need explicit approval and are not implied by an offline test pass.

Keep capture opt-in, keys encrypted and provider error states visible. Preserve the Jev check before displaying generated wording. Acoustic changes must retain quality gates and per-call references, without inferring emotions or speaker identity.
