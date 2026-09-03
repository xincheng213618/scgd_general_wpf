import fs from 'node:fs/promises'
import path from 'node:path'
import { localeOrder } from '../i18n/locales.mjs'
import { createNavItems, createSidebarItems } from '../i18n/navigation.mjs'
import { generateKnowledge } from './knowledge.mjs'
import { parseHtmlLinks, validateBuiltLinkFragments } from './html-links.mjs'

const root = process.cwd()
const docsRoot = path.join(root, 'docs')
const distRoot = path.join(docsRoot, '.vitepress', 'dist')
const configPath = path.join(docsRoot, '.vitepress', 'config.mts')
const manifestPath = path.join(distRoot, 'docs-manifest.json')
const searchIndexPath = path.join(distRoot, 'docs-search-index.json')

const skippedDirectoryNames = new Set(['.vitepress', 'node_modules'])
const skippedMarkdownFileNames = new Set(['agents.md'])
const archivedLocaleDirectories = ['en', 'zh-tw', 'ja', 'ko']
const genericSearchSectionTitles = new Set([
  '先查什么',
  '说明',
  '关键文件',
  '边界',
  '当前能力',
  '验收',
  '运行链路',
  '源码入口',
  '适用范围',
  '常见排查',
  '检查清单',
  '推荐阅读顺序',
  '发布验收',
  '常见使用顺序',
  '常见问题',
])
const forbiddenBuiltOutputPatterns = [
  { label: 'default VitePress not found title', pattern: /PAGE NOT FOUND/iu },
  { label: 'default VitePress not found home link', pattern: /Take me home/iu },
  { label: 'default VitePress not found quote', pattern: /But if you don't change your direction/iu },
  { label: 'archived English route', pattern: /(?:^|["'(\s])\/(?:scgd_general_wpf\/)?en\//iu },
  { label: 'old language navbar chunk', pattern: /VPNavBarTranslations/iu },
]

const failures = []
const indexedPageIds = new Map()
let knowledgeCatalog

async function main() {
  knowledgeCatalog = await generateKnowledge(root, true)
  await validateStrictBuildConfig()
  await validateArchivedLocales()

  const docsMarkdownFiles = await collectMarkdownFiles(docsRoot)
  const rootMarkdownFiles = await collectRootMarkdownFiles()
  const checkedMarkdownFiles = [...rootMarkdownFiles, ...docsMarkdownFiles]
  const redirectPages = await findRedirectPages(docsMarkdownFiles)

  await validateMarkdownTables(checkedMarkdownFiles)
  await validateMarkdownLinks(checkedMarkdownFiles, redirectPages)
  await validateNavigationLinks(redirectPages)
  await validateRedirectPages(redirectPages)
  await validateGeneratedIndexes(redirectPages)
  const builtLinks = await validateBuiltLinkFragments(distRoot)
  for (const failure of builtLinks.failures) fail(`docs/.vitepress/dist/${failure}`)
  await validateCustomNotFoundPage()
  await validateBuiltOutputPublicContent()
  await validatePageReachability(docsMarkdownFiles, redirectPages)

  if (failures.length > 0) {
    console.error(failures.join('\n'))
    process.exit(1)
  }

  console.log(`Validated ${checkedMarkdownFiles.length} markdown files and ${redirectPages.length} redirect compatibility pages.`)
  console.log(`Validated ${builtLinks.checkedLinks} distinct local fragment links across ${builtLinks.htmlFiles} built HTML files.`)
}

async function validateStrictBuildConfig() {
  const config = await fs.readFile(configPath, 'utf8')
  if (/ignoreDeadLinks\s*:\s*true/u.test(config)) {
    fail('docs/.vitepress/config.mts: ignoreDeadLinks must stay false so stale internal links fail the build.')
  }
}

async function validateArchivedLocales() {
  for (const localeDirectory of archivedLocaleDirectories) {
    if (await exists(path.join(docsRoot, localeDirectory))) {
      fail(`docs/${localeDirectory}: archived locale directory should stay out of the active docs tree.`)
    }
  }

}

async function collectMarkdownFiles(directory) {
  const entries = await fs.readdir(directory, { withFileTypes: true })
  const files = []

  for (const entry of entries) {
    if (skippedDirectoryNames.has(entry.name)) {
      continue
    }

    const entryPath = path.join(directory, entry.name)
    if (entry.isDirectory()) {
      files.push(...await collectMarkdownFiles(entryPath))
      continue
    }

    if (
      entry.isFile()
      && entry.name.toLowerCase().endsWith('.md')
      && !skippedMarkdownFileNames.has(entry.name.toLowerCase())
    ) {
      files.push(entryPath)
    }
  }

  return files
}

async function collectRootMarkdownFiles() {
  const rootMarkdownFileNames = ['README.md', 'CONTRIBUTING.md', 'LICENSE.md']
  const files = []

  for (const fileName of rootMarkdownFileNames) {
    const filePath = path.join(root, fileName)
    if (await exists(filePath)) {
      files.push(filePath)
    }
  }

  return files
}

async function validateMarkdownTables(markdownFiles) {
  for (const filePath of markdownFiles) {
    const content = await fs.readFile(filePath, 'utf8')
    const lines = content.split(/\r?\n/u)
    let fenced = false

    for (let index = 0; index < lines.length; index += 1) {
      const line = lines[index]
      if (/^\s*(```|~~~)/u.test(line)) {
        fenced = !fenced
        continue
      }

      if (fenced || !isMarkdownTableSeparator(line)) {
        continue
      }

      const expectedCellCount = splitTableCells(line).length
      const headerLine = lines[index - 1] ?? ''
      validateTableRow(filePath, index, headerLine, expectedCellCount, 'header')

      for (let rowIndex = index + 1; rowIndex < lines.length; rowIndex += 1) {
        const row = lines[rowIndex]
        if (!row.trim() || !row.includes('|') || /^\s*(```|~~~)/u.test(row)) {
          break
        }

        validateTableRow(filePath, rowIndex, row, expectedCellCount, 'row')
      }
    }
  }
}

function validateTableRow(filePath, zeroBasedLineNumber, line, expectedCellCount, rowKind) {
  const cellCount = splitTableCells(line).length
  if (cellCount !== expectedCellCount) {
    fail(`${relative(filePath)}:${zeroBasedLineNumber + 1}: markdown table ${rowKind} has ${cellCount} cells; expected ${expectedCellCount}.`)
  }
}

function isMarkdownTableSeparator(line) {
  const trimmed = line.trim()
  if (!trimmed.includes('|')) {
    return false
  }

  const cells = splitTableCells(trimmed)
  return cells.length >= 2 && cells.every((cell) => /^:?-{3,}:?$/u.test(cell.trim()))
}

function splitTableCells(line) {
  const trimmed = line.trim().replace(/^\|/u, '').replace(/\|$/u, '')
  const cells = []
  let current = ''
  let escaped = false

  for (const character of trimmed) {
    if (character === '|' && !escaped) {
      cells.push(current)
      current = ''
      escaped = false
      continue
    }

    current += character
    escaped = character === '\\' && !escaped
  }

  cells.push(current)
  return cells
}

async function validateMarkdownLinks(markdownFiles, redirectPages) {
  const redirectFilePaths = new Set(redirectPages.map((page) => page.filePath))
  const redirectRoutes = new Set(redirectPages.map((page) => docUrlForMarkdown(page.filePath)))

  for (const filePath of markdownFiles) {
    const content = await fs.readFile(filePath, 'utf8')
    const links = extractDocumentLinks(content)

    for (const link of links) {
      const target = normalizeLinkTarget(link)
      if (shouldSkipLinkTarget(target)) {
        continue
      }

      if (target.startsWith('/')) {
        const route = normalizeRoute(target)
        if (redirectRoutes.has(route) && !redirectFilePaths.has(filePath)) {
          fail(`${relative(filePath)}: active docs should not link to redirect compatibility route ${route}.`)
        }

        if (!(await builtRouteExists(route))) {
          fail(`${relative(filePath)}: missing site route ${target}`)
        }
        continue
      }

      const targetFilePath = await resolveMarkdownTarget(filePath, target)
      if (targetFilePath && redirectFilePaths.has(targetFilePath) && !redirectFilePaths.has(filePath)) {
        fail(`${relative(filePath)}: active docs should not link to redirect compatibility page ${target}.`)
      }

      if (!(await markdownTargetExists(filePath, target))) {
        fail(`${relative(filePath)}: missing markdown target ${target}`)
      }
    }
  }
}

function extractMarkdownLinks(markdownContent) {
  const links = []
  const pattern = /!?\[[^\]]*\]\(([^)]+)\)/gu
  for (const match of markdownContent.matchAll(pattern)) {
    links.push(match[1])
  }
  return links
}

function extractHtmlAttributeLinks(markdownContent) {
  const links = []
  const pattern = /\b(?:href|src)\s*=\s*["']([^"']+)["']/giu
  for (const match of markdownContent.matchAll(pattern)) {
    links.push(match[1])
  }
  return links
}

function extractDocumentLinks(markdownContent) {
  return [...extractMarkdownLinks(markdownContent), ...extractHtmlAttributeLinks(markdownContent)]
}

function normalizeLinkTarget(rawTarget) {
  let target = rawTarget.trim()

  if (target.startsWith('<') && target.endsWith('>')) {
    target = target.slice(1, -1)
  }

  target = target.split('#')[0].split('?')[0].trim()

  try {
    return decodeURIComponent(target)
  } catch {
    return target
  }
}

function shouldSkipLinkTarget(target) {
  if (!target || target.startsWith('#')) {
    return true
  }

  return /^(https?:|mailto:|tel:|app:|javascript:|data:)/iu.test(target)
}

async function markdownTargetExists(fromFile, target) {
  return (await resolveMarkdownTarget(fromFile, target)) !== null
}

async function resolveMarkdownTarget(fromFile, target) {
  const absolute = path.resolve(path.dirname(fromFile), target)
  if (await exists(absolute)) {
    return absolute
  }

  if (!path.extname(absolute)) {
    if (await exists(`${absolute}.md`)) {
      return `${absolute}.md`
    }

    if (await exists(path.join(absolute, 'README.md'))) {
      return path.join(absolute, 'README.md')
    }

    if (await exists(path.join(absolute, 'index.md'))) {
      return path.join(absolute, 'index.md')
    }
  }

  return null
}

async function validateNavigationLinks(redirectPages) {
  const redirectRoutes = new Set(redirectPages.map((page) => docUrlForMarkdown(page.filePath)))

  for (const localeKey of localeOrder) {
    const navigationItems = [
      ...flattenNavigationItems(createNavItems(localeKey)),
      ...flattenNavigationItems(createSidebarItems(localeKey)),
    ]

    for (const item of navigationItems) {
      if (!('link' in item)) {
        continue
      }

      const target = normalizeLinkTarget(item.link)
      if (shouldSkipNavigationTarget(target)) {
        continue
      }

      const route = normalizeRoute(target)
      if (redirectRoutes.has(route)) {
        fail(`navigation:${localeKey}: ${item.text} points at redirect compatibility route ${route}.`)
      }

      if (!(await builtRouteExists(route))) {
        fail(`navigation:${localeKey}: ${item.text} points at missing route ${route}.`)
      }
    }
  }
}

function flattenNavigationItems(items) {
  const flattenedItems = []

  for (const item of items) {
    flattenedItems.push(item)

    if (Array.isArray(item.items)) {
      flattenedItems.push(...flattenNavigationItems(item.items))
    }
  }

  return flattenedItems
}

function shouldSkipNavigationTarget(target) {
  if (shouldSkipLinkTarget(target)) {
    return true
  }

  return target.startsWith('/images/') || target.startsWith('/favicon')
}

async function findRedirectPages(markdownFiles) {
  const redirectPages = []

  for (const filePath of markdownFiles) {
    const content = await fs.readFile(filePath, 'utf8')
    const frontmatter = getFrontmatter(content)
    if (/^\s*redirect_from_deleted_page:\s*true\s*$/mu.test(frontmatter)) {
      redirectPages.push({ filePath, content, frontmatter })
    }
  }

  return redirectPages
}

async function validateRedirectPages(redirectPages) {
  for (const page of redirectPages) {
    if (!/^\s*search:\s*false\s*$/mu.test(page.frontmatter)) {
      fail(`${relative(page.filePath)}: redirect compatibility pages must set search: false.`)
    }

    const route = docUrlForMarkdown(page.filePath)
    if (!(await builtRouteExists(route))) {
      fail(`${relative(page.filePath)}: missing built HTML for redirect route ${route}`)
    } else if (!(await builtHtmlLooksValid(route))) {
      fail(`${relative(page.filePath)}: redirect route ${route} built as a 404 page.`)
    }

    const jsTargets = [...page.content.matchAll(/window\.location\.replace\('([^']+)'\)/gu)].map((match) => match[1])
    if (jsTargets.length === 0) {
      fail(`${relative(page.filePath)}: redirect compatibility page must include window.location.replace(...).`)
    }

    for (const target of jsTargets) {
      if (!(await builtRedirectTargetExists(page.filePath, target))) {
        fail(`${relative(page.filePath)}: missing redirect target route ${target}`)
      }
    }

    for (const rawTarget of extractDocumentLinks(page.content)) {
      const target = normalizeLinkTarget(rawTarget)
      if (shouldSkipLinkTarget(target)) {
        continue
      }

      if (target.startsWith('/')) {
        if (!(await builtRouteExists(target))) {
          fail(`${relative(page.filePath)}: missing redirect page site link ${target}`)
        }
        continue
      }

      if (!(await markdownTargetExists(page.filePath, target))) {
        fail(`${relative(page.filePath)}: missing redirect page markdown target ${target}`)
      }
    }
  }
}

async function validateGeneratedIndexes(redirectPages) {
  const manifest = JSON.parse(await fs.readFile(manifestPath, 'utf8'))
  const searchIndex = JSON.parse(await fs.readFile(searchIndexPath, 'utf8'))
  const redirectRelativePaths = new Set(redirectPages.map((page) => normalizePath(path.relative(docsRoot, page.filePath))))
  const expected = new Map(knowledgeCatalog.entries.map((entry) => [entry.knowledge_id, entry]))
  const manifestIds = new Set()

  for (const page of manifest.pages ?? []) {
    if (manifestIds.has(page.knowledge_id)) fail(`docs-manifest.json: duplicate knowledge_id ${page.knowledge_id}`)
    manifestIds.add(page.knowledge_id)
    if (redirectRelativePaths.has(page.relativePath)) {
      fail(`${page.relativePath}: redirect compatibility page leaked into docs-manifest.json.`)
    }
    await validateIndexedPage(page, expected, 'docs-manifest.json')
  }

  for (const entry of searchIndex.entries ?? []) {
    if (redirectRelativePaths.has(entry.relativePath)) {
      fail(`${entry.relativePath}: redirect compatibility page leaked into docs-search-index.json.`)
    }
    await validateIndexedPage(entry, expected, 'docs-search-index.json')
  }

  for (const id of expected.keys()) if (!manifestIds.has(id)) fail(`docs-manifest.json: missing knowledge topic ${id}`)
  for (const entry of knowledgeCatalog.entries.filter((item) => item.searchable)) {
    if (!(searchIndex.entries ?? []).some((item) => item.knowledge_id === entry.knowledge_id && item.kind === 'page')) {
      fail(`docs-search-index.json: missing searchable topic ${entry.knowledge_id}`)
    }
  }
  if (manifest.pagesCount !== (manifest.pages ?? []).length || manifest.pagesCount !== expected.size) fail('docs-manifest.json: inconsistent page count')
  if (searchIndex.entriesCount !== (searchIndex.entries ?? []).length) fail('docs-search-index.json: inconsistent entry count')

  validateSearchIndexEntries(searchIndex.entries ?? [])

}

async function validateIndexedPage(page, expected, artifact) {
  if (/(^|\/)agents\.md$/iu.test(page.relativePath ?? '')) fail(`${artifact}: AGENTS instructions must not be indexed as product knowledge`)
  const source = expected.get(page.knowledge_id)
  if (!source) { fail(`${artifact}: unknown knowledge_id ${page.knowledge_id}`); return }
  for (const field of ['status', 'knowledge_type', 'aliases', 'code_paths', 'test_paths', 'related']) {
    if (JSON.stringify(page[field]) !== JSON.stringify(source[field])) fail(`${artifact}: stale ${field} for ${page.knowledge_id}`)
  }
  if (page.sourcePath !== source.source || page.relativePath !== source.source.replace(/^docs\//u, '')) fail(`${artifact}: incorrect source mapping for ${page.knowledge_id}`)
  const route = normalizeLinkTarget(page.url)
  if (route !== source.url) fail(`${artifact}: incorrect URL for ${page.knowledge_id}`)
  if (!(await builtRouteExists(route)) || !(await builtHtmlLooksValid(route))) fail(`${artifact}: no real built HTML for ${page.url}`)
  const fragment = page.url.split('#').slice(1).join('#')
  if (fragment && await builtRouteExists(route)) {
    if (!indexedPageIds.has(route)) {
      const html = await fs.readFile(distPathForRoute(normalizeRoute(route)), 'utf8')
      indexedPageIds.set(route, parseHtmlLinks(html).ids)
    }
    let requested
    try { requested = decodeURIComponent(fragment) } catch { /* Invalid fragments cannot match an ID. */ }
    if (!indexedPageIds.get(route).has(requested)) fail(`${artifact}: missing built section anchor for ${page.url}`)
  }
}

function validateSearchIndexEntries(entries) {
  const entryIds = new Set()

  for (const entry of entries) {
    if (entryIds.has(entry.id)) {
      fail(`docs-search-index.json: duplicate search entry id ${entry.id}`)
    }
    entryIds.add(entry.id)

    if (entry.kind !== 'section') {
      continue
    }

    if (genericSearchSectionTitles.has(entry.title)) {
      fail(`docs-search-index.json: section search entry title is too generic: ${entry.title} (${entry.url})`)
    }

    if (!entry.title.includes('：')) {
      fail(`docs-search-index.json: section search entry title should include its page title: ${entry.title} (${entry.url})`)
    }
  }
}

async function validateCustomNotFoundPage() {
  const notFoundPath = path.join(distRoot, '404.html')
  if (!(await exists(notFoundPath))) {
    fail('docs/.vitepress/dist/404.html: missing generated not found page.')
    return
  }

  const html = await fs.readFile(notFoundPath, 'utf8')
  if (/PAGE NOT FOUND|Take me home|But if you don't change your direction/iu.test(html)) {
    fail('docs/.vitepress/dist/404.html: default VitePress not found copy leaked into the build.')
  }

  for (const requiredText of ['页面未找到', '返回文档首页']) {
    if (!html.includes(requiredText)) {
      fail(`docs/.vitepress/dist/404.html: missing custom not found text: ${requiredText}`)
    }
  }
}

async function validateBuiltOutputPublicContent() {
  const builtFiles = await collectBuiltFiles(distRoot, new Set(['.html', '.json']))

  for (const filePath of builtFiles) {
    const content = await fs.readFile(filePath, 'utf8')
    const fileLabel = relative(filePath)

    for (const { label, pattern } of forbiddenBuiltOutputPatterns) {
      if (pattern.test(content)) {
        fail(`${fileLabel}: forbidden built output content found: ${label}`)
      }
    }

  }
}

async function validatePageReachability(markdownFiles, redirectPages) {
  const routeToFile = new Map()
  const redirectRoutes = new Set(redirectPages.map((page) => docUrlForMarkdown(page.filePath)))

  for (const filePath of markdownFiles) {
    routeToFile.set(docUrlForMarkdown(filePath), filePath)
  }

  const graph = new Map()
  for (const filePath of markdownFiles) {
    const content = await fs.readFile(filePath, 'utf8')
    const route = docUrlForMarkdown(filePath)
    const linkedRoutes = []

    for (const rawTarget of extractDocumentLinks(content)) {
      const target = normalizeLinkTarget(rawTarget)
      if (shouldSkipLinkTarget(target)) {
        continue
      }

      const targetRoute = await resolveDocumentRoute(filePath, target)
      if (targetRoute && routeToFile.has(targetRoute)) {
        linkedRoutes.push(targetRoute)
      }
    }

    graph.set(route, linkedRoutes)
  }

  const startRoutes = new Set(['/'])
  for (const localeKey of localeOrder) {
    const navigationItems = [
      ...flattenNavigationItems(createNavItems(localeKey)),
      ...flattenNavigationItems(createSidebarItems(localeKey)),
    ]

    for (const item of navigationItems) {
      if (!('link' in item)) {
        continue
      }

      const target = normalizeLinkTarget(item.link)
      if (!shouldSkipNavigationTarget(target)) {
        startRoutes.add(normalizeRoute(target))
      }
    }
  }

  const reachableRoutes = new Set()
  const pendingRoutes = [...startRoutes]
  while (pendingRoutes.length > 0) {
    const route = pendingRoutes.pop()
    if (reachableRoutes.has(route)) {
      continue
    }

    reachableRoutes.add(route)
    for (const linkedRoute of graph.get(route) ?? []) {
      if (!reachableRoutes.has(linkedRoute)) {
        pendingRoutes.push(linkedRoute)
      }
    }
  }

  const unreachableRoutes = []
  for (const route of routeToFile.keys()) {
    if (redirectRoutes.has(route)) {
      continue
    }

    if (!reachableRoutes.has(route)) {
      unreachableRoutes.push(route)
    }
  }

  if (unreachableRoutes.length > 0) {
    fail(`Unreachable non-redirect docs pages:\n${unreachableRoutes.sort().map((route) => `  - ${route}`).join('\n')}`)
  }
}

async function collectBuiltFiles(directory, extensions) {
  const entries = await fs.readdir(directory, { withFileTypes: true })
  const files = []

  for (const entry of entries) {
    const entryPath = path.join(directory, entry.name)
    if (entry.isDirectory()) {
      files.push(...await collectBuiltFiles(entryPath, extensions))
      continue
    }

    if (entry.isFile() && extensions.has(path.extname(entry.name).toLowerCase())) {
      files.push(entryPath)
    }
  }

  return files
}

async function resolveDocumentRoute(fromFile, target) {
  if (target.startsWith('/')) {
    return normalizeRoute(target)
  }

  const absoluteTarget = path.resolve(path.dirname(fromFile), target)
  const candidates = [absoluteTarget]

  if (!path.extname(absoluteTarget)) {
    candidates.push(`${absoluteTarget}.md`)
    candidates.push(path.join(absoluteTarget, 'README.md'))
    candidates.push(path.join(absoluteTarget, 'index.md'))
  }

  for (const candidate of candidates) {
    if (!(await exists(candidate))) {
      continue
    }

    const stat = await fs.stat(candidate)
    if (stat.isDirectory()) {
      for (const indexName of ['README.md', 'index.md']) {
        const indexCandidate = path.join(candidate, indexName)
        if (await exists(indexCandidate)) {
          return routeIfInsideDocs(indexCandidate)
        }
      }
      continue
    }

    if (candidate.toLowerCase().endsWith('.md')) {
      return routeIfInsideDocs(candidate)
    }
  }

  return null
}

function routeIfInsideDocs(filePath) {
  const relativePath = path.relative(docsRoot, filePath)
  if (relativePath.startsWith('..') || path.isAbsolute(relativePath)) {
    return null
  }

  return docUrlForMarkdown(filePath)
}

function getFrontmatter(markdownContent) {
  return markdownContent.match(/^---\s*[\r\n]+([\s\S]*?)[\r\n]+---\s*/u)?.[1] ?? ''
}

function docUrlForMarkdown(filePath) {
  const relativePath = normalizePath(path.relative(docsRoot, filePath)).replace(/\.md$/iu, '')

  if (relativePath === 'index') {
    return '/'
  }

  if (relativePath.endsWith('/index')) {
    return `/${relativePath.slice(0, -'/index'.length)}/`
  }

  return `/${relativePath}`
}

async function builtRedirectTargetExists(fromFile, target) {
  const sourceRoute = docUrlForMarkdown(fromFile)
  const sourceDirectory = sourceRoute.slice(0, sourceRoute.lastIndexOf('/') + 1)
  const targetRoute = normalizeRoute(path.posix.normalize(path.posix.join(sourceDirectory, target)))
  return builtRouteExists(targetRoute)
}

async function builtRouteExists(route) {
  return exists(distPathForRoute(normalizeRoute(route)))
}

async function builtHtmlLooksValid(route) {
  const htmlPath = distPathForRoute(normalizeRoute(route))
  const html = await fs.readFile(htmlPath, 'utf8')
  return /<!doctype html/iu.test(html) && /id="app"/u.test(html) && !/class="VPNotFound"|PAGE NOT FOUND|Take me home/iu.test(html)
}

function distPathForRoute(route) {
  if (route === '/') {
    return path.join(distRoot, 'index.html')
  }

  if (route.endsWith('/')) {
    return path.join(distRoot, route.slice(1), 'index.html')
  }

  return path.join(distRoot, `${route.slice(1)}.html`)
}

function normalizeRoute(route) {
  const withoutBase = route.replace(/^\/scgd_general_wpf(?=\/|$)/u, '')
  const withLeadingSlash = withoutBase.startsWith('/') ? withoutBase : `/${withoutBase}`
  return withLeadingSlash.replace(/\/+/gu, '/')
}

function normalizePath(value) {
  return value.replace(/\\/gu, '/')
}

async function exists(filePath) {
  try {
    await fs.access(filePath)
    return true
  } catch {
    return false
  }
}

function relative(filePath) {
  return normalizePath(path.relative(root, filePath))
}

function fail(message) {
  failures.push(message)
}

await main()
