import { expect, test } from 'claude-code/testing'

import { checkPaths, checkSecrets, parseGit } from './rules'

// Built at runtime so this file itself never contains a key-shaped literal.
const FAKE_KEY = ['sk', 'ant', 'x'.repeat(24)].join('-')

// Fake git for a repo that is Arto (marker tracked) with the given staged files and diff.
function fakeGit(staged: string[], diff = '', isArto = true, untracked: string[] = []) {
  return (_: unknown, e: { argv: readonly string[] }) => {
    const a = e.argv.slice(1).join(' ')
    const ok = (stdout: string) => ({ value: { exitCode: 0, stdout, stderr: '' } })
    if (a.startsWith('rev-parse --show-toplevel')) return ok('/repo\n')
    if (a.startsWith('ls-files --others')) return ok(untracked.join('\n'))
    if (a.startsWith('ls-files')) return ok(isArto ? 'ArtoSalesCopilot.csproj\n' : '')
    if (a.includes('--cached --name-only')) return ok(staged.join('\n'))
    if (a.includes('--cached -U0')) return ok(diff)
    return ok('')
  }
}

test('blocks a commit with settings and a recording', async ($, on) => {
  on('process.run', fakeGit(['MainWindow.xaml', 'settings.json', 'sessions/call.wav']) as never)
  on('tool.call', () => ({ result: { text: 'ran' } }) as never)
  const r = await $.tool.call({ tool: 'Bash', command: 'git commit -m "x"' } as never)
  expect(String((r as { deny?: string }).deny)).toContain('settings.json')
  expect(String((r as { deny?: string }).deny)).toContain('sessions/call.wav')
})

test('lets a clean commit through', async ($, on) => {
  on('process.run', fakeGit(['MainWindow.xaml', 'brief-template.json', 'docs/redesign/demo.mp4']) as never)
  on('tool.call', () => ({ result: { text: 'ran' } }) as never)
  const r = await $.tool.call({ tool: 'Bash', command: 'git add -A && git commit -m "ok"' } as never)
  expect((r as { deny?: string }).deny).toBeUndefined()
})

test('ignores repositories that are not Arto', async ($, on) => {
  on('process.run', fakeGit(['settings.json'], '', false) as never)
  on('tool.call', () => ({ result: { text: 'ran' } }) as never)
  const r = await $.tool.call({ tool: 'Bash', command: 'git commit -m "x"' } as never)
  expect((r as { deny?: string }).deny).toBeUndefined()
})

test('checks the working tree when the same command runs git add', async ($, on) => {
  on('process.run', fakeGit([], '', true, ['local-data/settings.json']) as never)
  on('fs.read', () => ({ value: '{}' }) as never)
  on('tool.call', () => ({ result: { text: 'ran' } }) as never)
  const r = await $.tool.call({ tool: 'Bash', command: 'git add -A && git commit -m "x"' } as never)
  expect(String((r as { deny?: string }).deny)).toContain('local-data/settings.json')
})

test('rules', () => {
  expect(checkPaths(['brief-template.json'])).toEqual([])
  expect(checkPaths(['briefs/acme.json']).length).toBe(1)
  expect(checkPaths(['client-brief-acme.json', 'artur-base-brief.json', 'OpenAI-API.txt', 'HANDOFF.md', 'local-data/x.json']).length).toBe(5)
  expect(checkPaths(['models/yin.onnx', '.venv/lib/x.py', '.env.local']).length).toBe(3)
  expect(checkSecrets(`+++ b/App.cs\n+var key = "${FAKE_KEY}";`).length).toBe(1)
  expect(checkSecrets(`+++ b/App.cs\n-var key = "${FAKE_KEY}";`)).toEqual([])
  expect(parseGit('cd repo && git -c a=b commit -am "m"')).toEqual({ kind: 'commit', dir: 'repo', all: true })
  expect(parseGit('git push -u origin main')?.kind).toBe('push')
  expect(parseGit('git status')).toBeUndefined()
})
