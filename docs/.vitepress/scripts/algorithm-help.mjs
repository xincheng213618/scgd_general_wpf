import fs from 'node:fs/promises'
import path from 'node:path'
import { createHash } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import { createMarkdownRenderer } from 'vitepress'

export const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
export const outputPath = 'UI/ColorVision.UI.Desktop/Assets/Help'
export const sourceRegistryPath = 'docs/.vitepress/algorithm-help/sources.json'
const websiteRoot = 'https://xincheng213618.github.io/scgd_general_wpf/'
const startMarker = '<!-- algorithm-help:start -->'
const endMarker = '<!-- algorithm-help:end -->'
const componentPattern = /<GridDistortionDemo\s*\/>/gu

export function extractHelpSection(markdown) {
  const normalized = markdown.replace(/^\uFEFF/u, '').replace(/\r\n/gu, '\n')
  if (normalized.split(startMarker).length !== 2 || normalized.split(endMarker).length !== 2) {
    throw new Error('The canonical algorithm topic must contain exactly one algorithm-help marker pair.')
  }
  const start = normalized.indexOf(startMarker) + startMarker.length
  const end = normalized.indexOf(endMarker)
  if (end <= start || !normalized.slice(start, end).trim()) throw new Error('The algorithm-help section is empty or its markers are reversed.')
  return normalized.slice(start, end).trim()
}

function escapeHtml(value) {
  return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;')
}

function getOutline(html) {
  return [...html.matchAll(/<h([23])\s+id="([^"]+)"[^>]*>([\s\S]*?)<\/h\1>/gu)].map(([, level, id, content]) => ({
    level: Number(level),
    id,
    title: content.replace(/<a\b[^>]*class="header-anchor"[^>]*>[\s\S]*?<\/a>/gu, '').replace(/<[^>]+>/gu, '').replaceAll('\u200b', '').trim(),
  }))
}

function validateRenderedContent(html) {
  // Validate after Markdown/math rendering so inequalities and code such as ITemplate<T> stay ordinary content.
  const unknownComponent = html.match(/<\/?[A-Z][A-Za-z\d]*/u)
  if (unknownComponent) throw new Error(`Unsupported algorithm-help component: ${unknownComponent[0]}`)
  if (/<\/?(?:script|iframe|object|embed|link|base|form)\b/iu.test(html)) throw new Error('Algorithm-help content cannot supply executable or external-resource elements.')
}

function ensureOfflineResources(html) {
  for (const [, attributes] of html.matchAll(/<(?:script|img|source|link|image|use)\b([^>]+)>/giu)) {
    const resource = attributes.match(/\b(?:src|(?:xlink:)?href)=["']([^"']+)["']/iu)?.[1]
    if (resource && !resource.startsWith('#') && !resource.startsWith('data:image/')) throw new Error(`Offline help cannot depend on an external resource or an undelivered asset: ${resource}`)
  }
}

function rewriteArticleLinks(article, source, sources) {
  const localIds = new Set([...article.matchAll(/\bid="([^"]+)"/gu)].map(match => match[1]))
  return article.replace(/(<a\b[^>]*\bhref=")([^"]+)(")/gu, (match, prefix, href, suffix) => {
    if (/^(?:[a-z]+:|\/\/)/iu.test(href)) return match
    const [pathname, fragment] = href.split('#', 2)
    if (!pathname) {
      if (fragment && !localIds.has(decodeURI(fragment))) throw new Error(`Missing offline help anchor #${fragment} in ${source.sourcePath}.`)
      return match
    }
    const decoded = decodeURI(pathname)
    const targetPath = (decoded.startsWith('/') ? decoded.slice(1) : path.posix.normalize(path.posix.join(path.posix.dirname(source.sourcePath.slice('docs/'.length)), decoded))).replace(/\.(?:md|html)$/u, '')
    const target = sources.find(item => item.sourcePath.slice('docs/'.length, -'.md'.length) === targetPath)
    // Only point into another offline page when the requested heading is inside its exported excerpt.
    if (target && (!fragment || extractHelpSection(target.markdown).includes(`{#${decodeURI(fragment)}}`) || extractHelpSection(target.markdown).includes(`id="${decodeURI(fragment)}"`))) {
      return prefix + describeTopic(target).relativePath + (fragment ? '#' + fragment : '') + suffix
    }
    return prefix + websiteRoot + targetPath + (fragment ? '#' + fragment : '') + suffix
  })
}

function describeTopic(source) {
  const { sourcePath, markdown } = source
  if (typeof sourcePath !== 'string' || !sourcePath.startsWith('docs/') || !sourcePath.endsWith('.md') || sourcePath.includes('\\') || sourcePath.split('/').some(part => !part || part === '.' || part === '..')) throw new Error(`Invalid algorithm-help source path: ${sourcePath}`)
  const id = markdown.replace(/\r\n/gu, '\n').match(/^knowledge_id: "([a-z\d]+(?:[.-][a-z\d]+)*)"$/mu)?.[1]
  if (!id) throw new Error(`${sourcePath} must declare a stable knowledge_id.`)
  const title = extractHelpSection(markdown).match(/^## ([^\n]+?)\s*\{#algorithm-principles\}\s*$/mu)?.[1]
  if (!title) throw new Error(`${sourcePath} must have a level-two algorithm-principles heading.`)
  return { id, title: title.replace(/[`*_]/gu, ''), relativePath: `${id}.html`, onlineUrl: websiteRoot + sourcePath.slice('docs/'.length, -'.md'.length) + '#' + encodeURIComponent(source.onlineAnchor ?? 'algorithm-principles') }
}

/** Build one canonical excerpt. Metadata stays in its source topic instead of a second title/ID list. */
async function createTopicBundle({ source, sources, demoScript, demoCss, articleCss, bootScript, root = repositoryRoot }) {
  const { markdown, sourcePath } = source
  const topic = describeTopic(source)
  // A Windows Git checkout may use CRLF; line endings must not change the content hash or freshness result.
  demoScript = demoScript.replace(/\r\n/gu, '\n')
  demoCss = demoCss.replace(/\r\n/gu, '\n')
  articleCss = articleCss.replace(/\r\n/gu, '\n')
  bootScript = bootScript.replace(/\r\n/gu, '\n')
  const section = extractHelpSection(markdown)
  const renderer = await createMarkdownRenderer(path.join(root, 'docs'), { math: { svg: { fontCache: 'none' } } }, '/')
  const environment = { path: path.join(root, sourcePath), relativePath: sourcePath.slice('docs/'.length) }
  let article = renderer.render(section, environment).replace(componentPattern, '<section data-grid-distortion-demo></section>')
  if (environment.sfcBlocks?.scripts.length || environment.sfcBlocks?.styles.length || environment.sfcBlocks?.customBlocks.length) throw new Error('Algorithm-help content cannot supply executable or external-resource elements or SFC blocks.')
  validateRenderedContent(article)
  article = article.replace(/<table(\s[^>]*)?>/gu, '<div class="table-scroll"><table$1>').replaceAll('</table>', '</table></div>')
  article = rewriteArticleLinks(article, source, sources)
  ensureOfflineResources(article)
  const outline = getOutline(article)
  if (!outline.some(heading => heading.id === 'algorithm-principles')) throw new Error('The algorithm-help section must have an algorithm-principles heading anchor.')
  const navigation = outline.map(heading => `      <a${heading.level === 3 ? ' class="toc-sub"' : ''} href="#${escapeHtml(heading.id)}">${heading.title}</a>`).join('\n')
  const hasDemo = article.includes('<section data-grid-distortion-demo>')
  const sourceSha256 = createHash('sha256').update(JSON.stringify([section, articleCss, ...(hasDemo ? [demoScript, demoCss, bootScript] : [])])).digest('hex')
  const html = `<!doctype html>
<!-- Generated from ${sourcePath} by npm run docs:help. Do not edit generated files. -->
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'">
  <title>${escapeHtml(topic.title)} - ColorVision</title>
  <link rel="stylesheet" href="algorithm-help.css">
${hasDemo ? '  <link rel="stylesheet" href="grid-distortion-demo.css">\n  <script type="module" src="boot.js"></script>' : ''}
</head>
<body>
  <div class="help-layout">
    <nav class="help-toc" aria-label="本文目录">
      <p>本文目录</p>
${navigation}
    </nav>
    <main class="help-content">
      <p class="help-kicker">ColorVision · 算法原理 · 离线说明</p>
      <article>${article}</article>
      <p class="help-footer">此说明随软件提供。指标判定请核对项目约定的公式口径与限值。</p>
    </main>
  </div>
</body>
</html>
`
  return new Map([
    ['index.json', JSON.stringify([{ ...topic, sourceSha256 }], null, 2) + '\n'],
    [topic.relativePath, html],
    ['algorithm-help.css', articleCss],
    ['grid-distortion-demo.css', demoCss],
    ['grid-distortion-demo.js', demoScript],
    ['boot.js', bootScript],
  ])
}

/** Merge all registered topics into one delivered manifest and shared resource set. */
export async function createHelpBundle(inputs) {
  if (!Array.isArray(inputs.sources) || !inputs.sources.length) throw new Error('Register at least one algorithm-help source.')
  const bundle = new Map()
  const topics = []
  const sourcePaths = new Set()
  for (const source of inputs.sources) {
    const current = await createTopicBundle({ ...inputs, source })
    const [topic] = JSON.parse(current.get('index.json'))
    if (topics.some(item => item.id === topic.id) || sourcePaths.has(source.sourcePath)) throw new Error('Algorithm-help knowledge_id values and source paths must be unique.')
    topics.push(topic)
    sourcePaths.add(source.sourcePath)
    for (const [relative, content] of current) if (relative !== 'index.json') bundle.set(relative, content)
  }
  return new Map([['index.json', JSON.stringify(topics, null, 2) + '\n'], ...bundle])
}

export async function readHelpInputs(root = repositoryRoot) {
  const read = relative => fs.readFile(path.join(root, relative), 'utf8')
  const registrations = JSON.parse(await read(sourceRegistryPath))
  if (!Array.isArray(registrations)) throw new Error('The algorithm-help source registry must be an array.')
  const sources = await Promise.all(registrations.map(async registration => {
    const source = typeof registration === 'string' ? { sourcePath: registration } : registration
    const sourcePath = source?.sourcePath
    if (typeof sourcePath !== 'string' || !sourcePath.startsWith('docs/') || sourcePath.includes('..') || sourcePath.includes('\\') || !sourcePath.endsWith('.md')) throw new Error(`Invalid algorithm-help source path: ${sourcePath}`)
    return { ...source, markdown: await read(sourcePath) }
  }))
  const [demoScript, demoCss, articleCss, figureCss, bootScript] = await Promise.all([
    read('docs/.vitepress/components/grid-distortion-demo.mjs'),
    read('docs/.vitepress/components/grid-distortion-demo.css'),
    read('docs/.vitepress/algorithm-help/article.css'),
    read('docs/.vitepress/algorithm-help/figures.css'),
    read('docs/.vitepress/algorithm-help/boot.js'),
  ])
  return { sources, demoScript, demoCss, articleCss: articleCss + '\n' + figureCss, bootScript, root }
}

export async function writeHelpBundle(bundle, directory) {
  await fs.mkdir(directory, { recursive: true })
  for (const [relative, content] of bundle) await fs.writeFile(path.join(directory, relative), content, 'utf8')
}

export async function checkHelpBundle(bundle, directory) {
  const stale = []
  for (const [relative, content] of bundle) {
    let actual
    try { actual = await fs.readFile(path.join(directory, relative), 'utf8') }
    catch (error) { if (error.code !== 'ENOENT') throw error }
    if (actual?.replace(/\r\n/gu, '\n') !== content.replace(/\r\n/gu, '\n')) stale.push(relative)
  }
  if (stale.length) throw new Error(`Offline algorithm help is missing or stale: ${stale.join(', ')}. Run npm run docs:help.`)
}

async function main() {
  const command = process.argv[2] ?? 'generate'
  if (!['generate', 'check'].includes(command)) throw new Error('Usage: node docs/.vitepress/scripts/algorithm-help.mjs [generate|check]')
  const bundle = await createHelpBundle(await readHelpInputs())
  const directory = path.join(repositoryRoot, outputPath)
  if (command === 'check') await checkHelpBundle(bundle, directory)
  else await writeHelpBundle(bundle, directory)
  console.log(command === 'check' ? 'Offline algorithm help is current.' : `Generated ${bundle.size} offline algorithm help files.`)
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => { console.error(error.message); process.exitCode = 1 })
}
