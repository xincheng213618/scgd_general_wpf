import fs from 'node:fs/promises'
import path from 'node:path'
import { parseFrontmatter, validateRepositoryPath } from './knowledge.mjs'

// This is an explicit discovery boundary, not a classifier for every vendor or
// generated tree. It deliberately includes new/untracked READMEs without Git.
const skippedDirectories = new Set(['docs', 'bin', 'obj', 'node_modules', 'packages', 'vendor', 'third_party', 'third-party',
  'artifacts', 'testresults', 'log', 'logs', 'release', 'debug', 'x64', 'x86'])
const blank = (value) => value.replace(/[^\n]/gu, ' ')
const referenceKey = (value) => value.trim().replace(/\s+/gu, ' ').toLowerCase()
const unescapeMarkdown = (value) => value.replace(/\\([\\()[\]<> ])/gu, '$1')

function visibleMarkdown(markdown) {
  const text = markdown.replace(/\r\n/gu, '\n').replace(/^\uFEFF?---\n[\s\S]*?\n---(?:\n|$)/u, blank)
  // Consume in source order: comment markers inside code and code markers inside
  // comments must not hide unrelated links after the containing span.
  const markers = /^ {0,3}(`{3,}|~{3,})[^\n]*$|(`+)|<!--/gmu
  let output = ''
  let position = 0
  for (let match; (match = markers.exec(text)) !== null;) {
    let end
    if (match[1]) {
      const close = new RegExp(`^ {0,3}${match[1][0]}{${match[1].length},}[ \\t]*(?:\\n|$)`, 'gmu')
      close.lastIndex = markers.lastIndex
      const closing = close.exec(text)
      end = closing ? closing.index + closing[0].length : text.length
    } else if (match[2]) {
      if (escaped(text, match.index)) continue
      const close = /`+/gu
      close.lastIndex = markers.lastIndex
      for (let closing; (closing = close.exec(text)) !== null;) {
        if (closing[0].length === match[2].length) { end = close.lastIndex; break }
      }
      if (end === undefined) continue
    } else {
      const closing = text.indexOf('-->', markers.lastIndex)
      end = closing < 0 ? text.length : closing + 3
    }
    output += text.slice(position, match.index) + blank(text.slice(match.index, end))
    position = end
    markers.lastIndex = end
  }
  return output + text.slice(position)
}

function escaped(text, index) {
  let backslashes = 0
  while (index > 0 && text[--index] === '\\') backslashes += 1
  return backslashes % 2 === 1
}

// Read a common Markdown destination, including balanced parentheses and angle
// destinations with spaces. This is not a complete CommonMark parser.
function destination(text, start) {
  let index = start
  while (/[ \t]/u.test(text[index] ?? '') && index < text.length) index += 1
  const beginning = index
  if (text[index] === '<') {
    index += 1
    while (index < text.length && text[index] !== '\n') {
      if (text[index] === '>' && !escaped(text, index)) return { target: unescapeMarkdown(text.slice(beginning + 1, index)), end: index + 1 }
      index += 1
    }
    return null
  }
  let depth = 0
  while (index < text.length) {
    const char = text[index]
    if (char === '\\' && index + 1 < text.length) { index += 2; continue }
    if (/\s/u.test(char)) break
    if (char === '(') depth += 1
    if (char === ')') { if (depth === 0) break; depth -= 1 }
    index += 1
  }
  if (index === beginning || depth !== 0) return null
  return { target: unescapeMarkdown(text.slice(beginning, index)), end: index }
}

function inlineEnd(text, start) {
  let index = start
  while (/\s/u.test(text[index] ?? '') && index < text.length) index += 1
  if (['"', "'", '('].includes(text[index])) {
    const closing = text[index] === '(' ? ')' : text[index]
    index += 1
    while (index < text.length && (text[index] !== closing || escaped(text, index))) index += 1
    if (index === text.length) return -1
    index += 1
    while (/\s/u.test(text[index] ?? '') && index < text.length) index += 1
  }
  return text[index] === ')' ? index + 1 : -1
}

function decodeAttribute(value) {
  return value.replace(/&(?:amp|quot|apos|lt|gt|#\d+|#x[\da-f]+);/giu, (entity) => {
    const named = { '&amp;': '&', '&quot;': '"', '&apos;': "'", '&lt;': '<', '&gt;': '>' }
    if (named[entity.toLowerCase()]) return named[entity.toLowerCase()]
    const code = entity[2].toLowerCase() === 'x' ? Number.parseInt(entity.slice(3, -1), 16) : Number(entity.slice(2, -1))
    return code > 0 && code <= 0x10ffff ? String.fromCodePoint(code) : entity
  })
}

/** Common inline/reference/quoted HTML links outside frontmatter, fenced and
 * inline code, and HTML comments. Not full CommonMark or fragment validation. */
export function scanReadmeLinks(markdown) {
  const visible = visibleMarkdown(markdown)
  const links = []
  const references = new Map()
  const lineAt = (index) => visible.slice(0, index).split('\n').length
  const body = visible.replace(/^ {0,3}\[([^\]\n]+)\]:[ \t]*(.*)$/gmu, (definition, label, rest) => {
    const parsed = destination(rest, 0)
    if (parsed && !references.has(referenceKey(label))) references.set(referenceKey(label), parsed.target)
    return blank(definition)
  })
  const labels = /!?\[([^\]\n]*)\]/gu
  for (let match; (match = labels.exec(body)) !== null;) {
    if (escaped(body, match.index)) continue
    const after = match.index + match[0].length
    let target
    if (body[after] === '(') {
      const parsed = destination(body, after + 1)
      const end = parsed && inlineEnd(body, parsed.end)
      if (end >= 0 && parsed) { target = parsed.target; labels.lastIndex = end }
    } else if (body[after] === '[') {
      const reference = /^\[([^\]\n]*)\]/u.exec(body.slice(after))
      if (reference) {
        target = references.get(referenceKey(reference[1] || match[1]))
        labels.lastIndex = after + reference[0].length
      }
    } else {
      target = references.get(referenceKey(match[1]))
    }
    if (target) links.push({ target, line: lineAt(match.index) })
  }
  for (const tag of body.matchAll(/<[a-z][\w:-]*(?=\s|\/?>)(?:[^"'<>]|"[^"]*"|'[^']*')*>/giu)) {
    for (const attribute of tag[0].matchAll(/\s+([^\s=<>/"']+)(?:\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s"'=<>`]+)))?/gu)) {
      if (!['href', 'src'].includes(attribute[1].toLowerCase())) continue
      const target = attribute[2] ?? attribute[3] ?? attribute[4]
      if (target) links.push({ target: decodeAttribute(target), line: lineAt(tag.index) })
    }
  }
  return links.sort((left, right) => left.line - right.line)
}

function inside(root, target) {
  const relative = path.relative(root, target)
  return relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative)
}

function docsRelativeTarget(repoRoot, source, target) {
  if (/^(?:[a-z][a-z\d+.-]*:|\/\/|#)/iu.test(target)) return null
  const rawPath = target.split('#')[0].split('?')[0]
  if (!rawPath) return null
  let decoded
  try { decoded = decodeURIComponent(rawPath) } catch {
    if (/(?:^|\/)docs(?:\/|$)/iu.test(rawPath)) throw new Error(`invalid percent encoding: ${target}`)
    return null
  }
  const portable = decoded.replace(/\\/gu, '/')
  if (portable.startsWith('/') && !/^\/docs(?:\/|$)/iu.test(portable)) return null
  const absolute = portable.startsWith('/') ? path.resolve(repoRoot, `.${portable}`) : path.resolve(path.dirname(source), portable)
  const relative = path.relative(repoRoot, absolute).replace(/\\/gu, '/')
  if (!/^docs(?:\/|$)/iu.test(relative)) return null
  if (decoded.includes('\\') || decoded.includes('\0')) throw new Error(`invalid documentation path: ${target}`)
  return { relative, directory: /(?:\/|(?:^|\/)\.{1,2})$/u.test(portable) }
}

async function resolveDocsTarget(repoRoot, docsRoot, { relative, directory }, cache) {
  const candidates = directory ? [] : [relative]
  if (!directory) {
    if (/\.html?$/iu.test(relative)) candidates.push(relative.replace(/\.html?$/iu, '.md'))
    else if (!/\.md$/iu.test(relative)) candidates.push(`${relative}.md`)
  }
  candidates.push(path.posix.join(relative, 'README.md'), path.posix.join(relative, 'index.md'))
  for (const candidate of candidates) {
    const absolute = path.resolve(repoRoot, candidate)
    try { await fs.lstat(absolute) } catch (error) {
      if (['ENOENT', 'ENOTDIR'].includes(error.code)) continue
      throw error
    }
    const info = await validateRepositoryPath(repoRoot, candidate, 'README docs target', cache)
    const actual = await fs.realpath(absolute)
    if (!inside(docsRoot, actual)) throw new Error(`resolved documentation target escapes docs: ${relative}`)
    if (!info.isDirectory) return absolute
  }
  throw new Error(`missing documentation page or directory entry: ${relative}`)
}

/** Scan the explicitly bounded repository tree, not Git or the knowledge topic
 * set. Source-relative links outside docs and fragments are outside this check. */
export async function validateReadmeDocsLinks(repoRoot) {
  const root = await fs.realpath(path.resolve(repoRoot))
  const docsRoot = await fs.realpath(path.join(root, 'docs'))
  if (!inside(root, docsRoot)) throw new Error('resolved docs directory escapes repository')
  const failures = []
  const readmes = []
  async function walk(directory) {
    for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
      const name = entry.name.toLowerCase()
      if (entry.name.startsWith('.') || skippedDirectories.has(name) || (directory === root && name === 'dll')) continue
      const absolute = path.join(directory, entry.name)
      if (entry.isSymbolicLink()) {
        if (name === 'readme.md') failures.push(`${path.relative(root, absolute)}: source README symlink is not read`)
        continue
      }
      if (entry.isDirectory()) await walk(absolute)
      else if (entry.isFile() && name === 'readme.md') readmes.push(absolute)
    }
  }
  await walk(root)
  const cache = new Map()
  let links = 0
  for (const source of readmes.sort()) {
    const label = path.relative(root, source).replace(/\\/gu, '/')
    const stat = await fs.lstat(source)
    if (stat.isSymbolicLink() || !inside(root, await fs.realpath(source))) {
      failures.push(`${label}: source README symlink or escaped path is not read`)
      continue
    }
    for (const link of scanReadmeLinks(await fs.readFile(source, 'utf8'))) {
      try {
        const resolved = docsRelativeTarget(root, source, link.target)
        if (resolved === null) continue
        links += 1
        const target = await resolveDocsTarget(root, docsRoot, resolved, cache)
        if (/\.md$/iu.test(target) && parseFrontmatter(await fs.readFile(target, 'utf8'), resolved.relative).redirect) {
          throw new Error(`link points to retired redirect compatibility page: ${resolved.relative}`)
        }
      } catch (error) { failures.push(`${label}:${link.line}: ${error.message} (${link.target})`) }
    }
  }
  if (failures.length) throw new Error(`README docs links: ${readmes.length} READMEs, ${links} docs links; ${failures.length} failure(s).\n${failures.join('\n')}`)
  return { readmes: readmes.length, links }
}
