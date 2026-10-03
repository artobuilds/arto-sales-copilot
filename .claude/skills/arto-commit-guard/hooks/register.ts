import type { Register } from 'claude-code'

import { checkPaths, checkSecrets, parseGit } from './rules'
import type { Finding } from './rules'

// Only repositories that carry this project file are guarded.
const MARKER = 'ArtoSalesCopilot.csproj'

export const register: Register = on => {
  let enabled = true

  on('session.start', async ($, e, next) => {
    await $.command.register({
      name: 'arto-guard',
      description: 'Arto commit guard: /arto-guard on | off | status',
    })
    return next(e)
  })

  on('command.run', { command: 'arto-guard' }, async ($, e) => {
    const arg = e.args.trim().toLowerCase()
    // Only the person (typed at the prompt or sent from the app) may switch it off, never a model or plugin.
    const byPerson = e.origin.kind === 'composer' || e.origin.kind === 'bridge'
    if (arg === 'off' && !byPerson) return { text: 'Arto commit guard can only be turned off by the user.' }
    if (arg === 'off') enabled = false
    else if (arg === 'on') enabled = true
    else if (arg && arg !== 'status') return { text: 'Usage: /arto-guard on | off | status' }
    $.ui.status(enabled ? undefined : 'Arto guard: OFF')
    return { text: `Arto commit guard is ${enabled ? 'on' : 'off'}${enabled ? '' : ' for this session (resets on reload)'}.` }
  })

  on('tool.call', { tool: 'Bash' }, async ($, e, next) => {
    if (!enabled) return next(e)
    const action = parseGit(e.command)
    if (!action) return next(e)

    const git = async (...args: string[]) => {
      const r = await $.process.run(['git', ...args], { cwd: action.dir, timeoutMs: 20000 })
      return r.exitCode === 0 ? r.stdout : undefined
    }

    const top = (await git('rev-parse', '--show-toplevel'))?.trim()
    if (!top) return next(e)
    const marker = await git('ls-files', '--', MARKER)
    if (!marker?.trim()) return next(e)

    let paths: string[] = []
    let diff = ''
    if (action.kind === 'commit') {
      paths = lines(await git('diff', '--cached', '--name-only', '--diff-filter=ACMR'))
      diff = (await git('diff', '--cached', '-U0', '--diff-filter=ACMR')) ?? ''
      // The hook runs before the command, so `git add … && git commit` has staged nothing yet:
      // then the working tree is what will be committed, and it is checked too.
      const adds = /(^|[\s;&|])git(\s+-[cC]\s+\S+)*\s+add\b/.test(e.command)
      if (action.all || adds) {
        paths.push(...lines(await git('diff', '--name-only', '--diff-filter=ACMR')))
        diff += (await git('diff', '-U0', '--diff-filter=ACMR')) ?? ''
      }
      if (adds) {
        const untracked = lines(await git('ls-files', '--others', '--exclude-standard'))
        paths.push(...untracked)
        for (const path of untracked.slice(0, 300)) {
          try {
            const text = await $.fs.read(`${top}/${path}`)
            diff += `\n+++ b/${path}\n` + text.split('\n').map(l => `+${l}`).join('\n')
          } catch {
            // Missing, binary or over 4 MiB: the path rules still apply.
          }
        }
      }
    } else {
      const base = (await git('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}'))?.trim()
        ?? (await git('rev-parse', '--abbrev-ref', 'origin/HEAD'))?.trim()
      if (!base) {
        $.ui.toast('Arto guard: no upstream to compare with, push not checked')
        return next(e)
      }
      paths = lines(await git('diff', '--name-only', '--diff-filter=ACMR', `${base}...HEAD`))
      diff = (await git('diff', '-U0', '--diff-filter=ACMR', `${base}...HEAD`)) ?? ''
    }

    const findings = dedupe([...checkPaths([...new Set(paths)]), ...checkSecrets(diff)])
    if (findings.length === 0) return next(e)

    $.ui.toast(`Arto guard blocked git ${action.kind}: ${findings.length} file(s)`)
    const list = findings.slice(0, 20).map(f => `- ${f.path} (${f.reason})`).join('\n')
    return {
      deny:
        `arto-commit-guard blocked \`git ${action.kind}\`: AGENTS.md forbids committing credentials, settings, briefs, transcripts, recordings, provider responses, model weights or runtimes.\n` +
        `${list}${findings.length > 20 ? `\n…and ${findings.length - 20} more` : ''}\n` +
        (action.kind === 'commit'
          ? 'Unstage these files (git restore --staged <file>) and add them to .gitignore if needed. '
          : 'Remove these files from the unpushed commits before pushing. ') +
        'If a file is a false positive, ask the user; only the user can turn the guard off with /arto-guard off.',
    }
  })
}

function lines(text: string | undefined): string[] {
  return (text ?? '').split('\n').map(s => s.trim()).filter(Boolean)
}

function dedupe(list: Finding[]): Finding[] {
  return list.filter((f, i) => list.findIndex(g => g.path === f.path) === i)
}
