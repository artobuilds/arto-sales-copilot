// Pure rules, kept apart from the hooks so they are easy to test and extend.
// Source: AGENTS.md of Arto Sales Copilot ("Never commit credentials, settings,
// personal/client briefs, transcripts, recordings, provider responses, model
// weights or runtime environments").

export type Finding = { path: string; reason: string }

const PATH_RULES: { re: RegExp; reason: string }[] = [
  { re: /(^|\/)\.env(\.|$)/i, reason: 'environment file' },
  { re: /\.(pem|key|pfx|p12|dpapi)$/i, reason: 'key or certificate' },
  { re: /(^|\/)(user)?settings(\.[\w-]+)?\.json$/i, reason: 'app settings' },
  { re: /(^|\/)[^/]*brief(?!-template\.json$)[^/]*\.json$/i, reason: 'client brief' },
  { re: /(^|\/)briefs?\//i, reason: 'client brief' },
  { re: /API\.txt$/, reason: 'API key file' },
  { re: /(^|\/)(PROFILE-SOURCES|HANDOFF)\.md$/, reason: 'private notes' },
  { re: /(^|\/)(local-data|test-output|\.models)\//i, reason: 'local data or test output' },
  { re: /\.(log|zip)$/i, reason: 'log or archive' },
  { re: /transcript/i, reason: 'transcript' },
  { re: /(^|\/)(sessions?|recordings?)\//i, reason: 'session or recording folder' },
  { re: /(^|\/)(provider[-_]?)?responses?\//i, reason: 'provider response' },
  { re: /\.response\.json$/i, reason: 'provider response' },
  { re: /\.(onnx|gguf|safetensors|pt|pth|ckpt|tflite)$/i, reason: 'model weights' },
  { re: /(^|\/)models?\/.*\.bin$/i, reason: 'model weights' },
  { re: /(^|\/)(\.?venv|site-packages|python-runtime|runtime)\//i, reason: 'runtime environment' },
]

// Audio/video is only allowed as documentation (synthetic demo renders under docs/).
const MEDIA = /\.(wav|mp3|m4a|flac|ogg|opus|webm|mp4|mkv|avi|mov)$/i

const SECRET_RULES: { re: RegExp; reason: string }[] = [
  { re: /sk-ant-[A-Za-z0-9_-]{20,}/, reason: 'Anthropic API key' },
  { re: /sk-(proj-)?[A-Za-z0-9_-]{20,}/, reason: 'OpenAI-style API key' },
  { re: /-----BEGIN [A-Z ]*PRIVATE KEY-----/, reason: 'private key' },
  { re: /\b(api[_-]?key|secret|token|password)\b["']?\s*[:=]\s*["'][^"'\s]{12,}["']/i, reason: 'hard-coded secret' },
]

export function checkPaths(paths: readonly string[]): Finding[] {
  const found: Finding[] = []
  for (const path of paths) {
    const rule = PATH_RULES.find(r => r.re.test(path))
    if (rule) found.push({ path, reason: rule.reason })
    else if (MEDIA.test(path) && !/^docs\//i.test(path)) found.push({ path, reason: 'recording outside docs/' })
  }
  return found
}

// `diff` is unified diff text; only added lines are inspected. Values never leave this function.
export function checkSecrets(diff: string): Finding[] {
  const found: Finding[] = []
  let file = '?'
  for (const line of diff.split('\n')) {
    if (line.startsWith('+++ ')) { file = line.replace(/^\+\+\+ (b\/)?/, ''); continue }
    if (!line.startsWith('+')) continue
    const rule = SECRET_RULES.find(r => r.re.test(line))
    if (rule && !found.some(f => f.path === file && f.reason === rule.reason)) found.push({ path: file, reason: rule.reason })
  }
  return found
}

export type GitAction = { kind: 'commit' | 'push'; dir?: string; all: boolean }

// Best effort: finds `git commit` / `git push` in a shell line, with `cd X &&` or `git -C X`.
export function parseGit(command: string): GitAction | undefined {
  for (const part of command.split(/&&|\|\||;|\n/)) {
    const m = part.match(/(?:^|\s)git((?:\s+-[cC]\s+\S+|\s+--[\w-]+(?:=\S+)?)*)\s+(commit|push)\b(.*)$/)
    if (!m) continue
    const [, opts = '', verb = '', rest = ''] = m
    const dir = opts.match(/-C\s+(\S+)/)?.[1] ?? command.match(/(?:^|&&|;)\s*cd\s+("[^"]+"|\S+)/)?.[1]?.replace(/"/g, '')
    const all = verb === 'commit' && /(^|\s)(-a|--all|-[a-zA-Z]*a[a-zA-Z]*)(\s|$)/.test(rest)
    return { kind: verb as 'commit' | 'push', dir, all }
  }
  return undefined
}
