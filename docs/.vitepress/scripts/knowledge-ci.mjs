import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { execFileSync } from 'node:child_process'

export function affectsWebsite(changedPaths) {
  return changedPaths.some((source) => source.startsWith('docs/')
    || /(^|\/)AGENTS\.md$/u.test(source)
    || ['package.json', 'package-lock.json', '.github/workflows/deploy.yml', 'README.md', 'CONTRIBUTING.md', 'LICENSE.md'].includes(source))
}

async function main() {
  let websiteChanged = true
  if (process.env.KNOWLEDGE_EVENT_NAME === 'pull_request') {
    const base = process.env.KNOWLEDGE_BASE_SHA
    const head = process.env.KNOWLEDGE_HEAD_SHA
    if (![base, head].every((sha) => /^[0-9a-f]{40,64}$/u.test(sha ?? ''))) throw new Error('Missing or invalid PR commit SHA; cannot safely determine website scope')
    const changed = execFileSync('git', ['diff', '--name-only', '-z', `${base}...${head}`], { encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 }).split('\0').filter(Boolean)
    websiteChanged = affectsWebsite(changed)
  }
  if (!process.env.GITHUB_OUTPUT) throw new Error('knowledge-ci.mjs is intended for GitHub Actions; GITHUB_OUTPUT is required')
  await fs.appendFile(process.env.GITHUB_OUTPUT, `website_changed=${websiteChanged}\n`)
  console.log(`Website build required: ${websiteChanged}. Every PR still runs the dependency-free knowledge checks.`)
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => { console.error(error.message); process.exitCode = 1 })
}
