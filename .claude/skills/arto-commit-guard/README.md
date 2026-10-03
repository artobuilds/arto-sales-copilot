# arto-commit-guard

A Claude Code mod (plugin with function hooks) that enforces the data rule in `AGENTS.md` when Claude Code commits or pushes in this repository.

Before a `git commit` or `git push` that Claude Code runs, it checks the files going out:

- by path: settings, `.env`, keys and certificates, client briefs (not `brief-template.json`), transcripts, `sessions/` and `recordings/`, provider responses, model weights, Python runtimes, local data, logs, archives, and audio/video outside `docs/`;
- by added lines: API keys, private keys and hard-coded secrets (values are never printed).

When the same command also runs `git add` (or `commit -a`), the working tree is checked too, since nothing is staged yet when the hook runs.

On a match the command is refused with the list of files and why. It only acts in a repository that tracks `ArtoSalesCopilot.csproj`.

`/arto-guard status | on | off` — only the person (typed at the prompt or sent from the app) can turn it off; it stays off until the session reloads.

Limits: it sees only commands Claude Code runs, not commits made in a terminal or IDE by hand, and it reads shell lines on a best-effort basis. It is an extra layer, not a replacement for inspecting `git diff --cached` (see `CONTRIBUTING.md`).

Check: `claude plugin validate .claude/skills/arto-commit-guard` and `claude plugin test .claude/skills/arto-commit-guard`. Tests use fake git output only.
