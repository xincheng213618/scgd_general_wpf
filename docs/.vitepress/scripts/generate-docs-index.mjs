import fs from 'node:fs/promises'
import path from 'node:path'

const docsRoot = path.resolve(process.cwd(), 'docs')
const distRoot = path.join(docsRoot, '.vitepress', 'dist')
const manifestOutputPath = path.join(distRoot, 'docs-manifest.json')
const searchIndexOutputPath = path.join(distRoot, 'docs-search-index.json')

const sectionDefinitions = [
  { key: 'root', title: '首页与入口', url: '/' },
  { key: '00-getting-started', title: '安装与首次使用', url: '/00-getting-started/README' },
  { key: '01-user-guide', title: '日常使用', url: '/01-user-guide/README' },
  { key: '02-developer-guide', title: '开发与交付', url: '/02-developer-guide/README' },
  { key: '03-architecture', title: '设计与架构', url: '/03-architecture/README' },
  { key: '04-api-reference', title: 'API 参考', url: '/04-api-reference/README' },
  { key: '05-resources', title: '附录与资源', url: '/05-resources/README' },
]

const sectionTitleMap = new Map(sectionDefinitions.map((definition) => [definition.key, definition.title]))

async function main() {
  await ensureDistDirectory()

  const markdownFiles = await collectMarkdownFiles(docsRoot)
  const pages = []
  const searchEntries = []

  for (const markdownFilePath of markdownFiles) {
    const page = await buildPageRecord(markdownFilePath)
    pages.push(page)
    searchEntries.push(...buildSearchEntries(page))
  }

  pages.sort((left, right) => left.relativePath.localeCompare(right.relativePath, 'zh-CN'))
  searchEntries.sort((left, right) => left.url.localeCompare(right.url, 'zh-CN'))

  const sections = buildSections(pages)
  const generatedAt = new Date().toISOString()

  const manifest = {
    generatedAt,
    basePath: '/scgd_general_wpf/',
    pagesCount: pages.length,
    entriesCount: searchEntries.length,
    sections,
    pages: pages.map((page) => ({
      title: page.title,
      summary: page.summary,
      url: page.url,
      relativePath: page.relativePath,
      sourcePath: page.sourcePath,
      sectionKey: page.sectionKey,
      sectionTitle: page.sectionTitle,
      wordCount: page.wordCount,
      headings: page.headings,
    })),
  }

  const searchIndex = {
    generatedAt,
    basePath: '/scgd_general_wpf/',
    entriesCount: searchEntries.length,
    pagesCount: pages.length,
    entries: searchEntries,
  }

  await Promise.all([
    fs.writeFile(manifestOutputPath, JSON.stringify(manifest, null, 2), 'utf8'),
    fs.writeFile(searchIndexOutputPath, JSON.stringify(searchIndex, null, 2), 'utf8'),
  ])

  console.log(`Generated ${path.relative(process.cwd(), manifestOutputPath)}`)
  console.log(`Generated ${path.relative(process.cwd(), searchIndexOutputPath)}`)
  console.log(`Indexed ${pages.length} pages and ${searchEntries.length} searchable entries.`)
}

async function ensureDistDirectory() {
  await fs.mkdir(distRoot, { recursive: true })
}

async function collectMarkdownFiles(rootDirectory) {
  const entries = await fs.readdir(rootDirectory, { withFileTypes: true })
  const filePaths = []

  for (const entry of entries) {
    if (shouldSkipEntry(entry.name)) {
      continue
    }

    const entryPath = path.join(rootDirectory, entry.name)
    if (entry.isDirectory()) {
      filePaths.push(...await collectMarkdownFiles(entryPath))
      continue
    }

    if (entry.isFile() && entry.name.toLowerCase().endsWith('.md')) {
      filePaths.push(entryPath)
    }
  }

  return filePaths
}

function shouldSkipEntry(name) {
  if (name === '.vitepress' || name === 'node_modules') {
    return true
  }

  if (name.startsWith('_')) {
    return true
  }

  if (name.startsWith('._') || name.startsWith('~')) {
    return true
  }

  if (name.endsWith('.bak') || name.endsWith('.tmp')) {
    return true
  }

  return false
}

async function buildPageRecord(markdownFilePath) {
  const rawContent = await fs.readFile(markdownFilePath, 'utf8')
  const relativePath = normalizePath(path.relative(docsRoot, markdownFilePath))
  const sourcePath = normalizePath(path.join('docs', relativePath))
  const url = toDocumentUrl(relativePath)
  const sectionKey = getSectionKey(relativePath)
  const sectionTitle = sectionTitleMap.get(sectionKey) ?? sectionKey
  const parsedMarkdown = parseMarkdown(rawContent)
  const title = parsedMarkdown.title || fallbackTitleFromPath(relativePath)
  const summary = createSummary(parsedMarkdown.summaryText)
  const headings = parsedMarkdown.headings.map((heading) => ({
    depth: heading.depth,
    text: heading.text,
    slug: heading.slug,
    url: heading.depth === 1 ? url : `${url}#${heading.slug}`,
  }))

  return {
    title,
    summary,
    url,
    relativePath,
    sourcePath,
    sectionKey,
    sectionTitle,
    wordCount: countWords(parsedMarkdown.plainText),
    headings,
    sections: parsedMarkdown.sections.map((section) => ({
      title: section.title,
      slug: section.slug,
      depth: section.depth,
      titles: section.titles,
      text: section.text,
      summary: createSummary(section.text),
      url: section.slug ? `${url}#${section.slug}` : url,
    })),
  }
}

function parseMarkdown(markdownContent) {
  const contentWithoutFrontmatter = markdownContent.replace(/^---\s*[\r\n]+[\s\S]*?[\r\n]+---\s*/u, '')
  const lines = contentWithoutFrontmatter.split(/\r?\n/)
  const slugCounts = new Map()
  const headings = []
  const sections = []
  const headingStack = []
  const summaryLines = []

  let pageTitle = ''
  let currentSection = createSection('', '', 1, [])
  let inCodeFence = false

  for (const rawLine of lines) {
    const trimmedLine = rawLine.trim()

    if (trimmedLine.startsWith('```')) {
      inCodeFence = !inCodeFence
      continue
    }

    if (!inCodeFence) {
      const headingMatch = /^(#{1,6})\s+(.*)$/u.exec(trimmedLine)
      if (headingMatch) {
        flushSection(sections, currentSection)

        const depth = headingMatch[1].length
        const headingText = normalizeInlineText(headingMatch[2])
        const slug = createSlug(headingText, slugCounts)

        while (headingStack.length >= depth) {
          headingStack.pop()
        }

        headingStack.push(headingText)
        headings.push({ depth, text: headingText, slug })

        if (!pageTitle && depth === 1) {
          pageTitle = headingText
        }

        currentSection = createSection(
          depth === 1 ? '' : headingText,
          depth === 1 ? '' : slug,
          depth,
          depth === 1 ? [headingText] : [...headingStack],
        )

        continue
      }
    }

    const normalizedLine = normalizeMarkdownLine(rawLine)
    if (!normalizedLine) {
      currentSection.lines.push('')
      continue
    }

    if (summaryLines.length < 5) {
      summaryLines.push(normalizedLine)
    }

    currentSection.lines.push(normalizedLine)
  }

  flushSection(sections, currentSection)

  const plainText = sections
    .map((section) => section.text)
    .filter(Boolean)
    .join('\n')
    .trim()

  return {
    title: pageTitle,
    headings,
    sections,
    plainText,
    summaryText: summaryLines.join(' ').trim() || plainText,
  }
}

function createSection(title, slug, depth, titles) {
  return {
    title,
    slug,
    depth,
    titles,
    lines: [],
  }
}

function flushSection(targetSections, section) {
  const text = normalizeWhitespace(section.lines.join('\n'))
  if (!text) {
    return
  }

  targetSections.push({
    title: section.title,
    slug: section.slug,
    depth: section.depth,
    titles: section.titles.filter(Boolean),
    text,
  })
}

function buildSearchEntries(page) {
  const entries = []

  entries.push({
    id: page.url,
    kind: 'page',
    sectionKey: page.sectionKey,
    sectionTitle: page.sectionTitle,
    title: page.title,
    titles: [page.title],
    text: page.summary,
    url: page.url,
    relativePath: page.relativePath,
  })

  for (const section of page.sections) {
    entries.push({
      id: section.url,
      kind: 'section',
      sectionKey: page.sectionKey,
      sectionTitle: page.sectionTitle,
      title: section.title || page.title,
      titles: section.titles.length > 0 ? section.titles : [page.title],
      text: section.text,
      summary: section.summary,
      url: section.url,
      relativePath: page.relativePath,
    })
  }

  return entries
}

function buildSections(pages) {
  const pagesBySection = new Map(sectionDefinitions.map((definition) => [definition.key, []]))

  for (const page of pages) {
    if (!pagesBySection.has(page.sectionKey)) {
      pagesBySection.set(page.sectionKey, [])
    }

    pagesBySection.get(page.sectionKey).push(page)
  }

  return [...pagesBySection.entries()]
    .map(([key, sectionPages]) => ({
      key,
      title: sectionTitleMap.get(key) ?? key,
      url: sectionDefinitions.find((definition) => definition.key === key)?.url ?? sectionPages[0]?.url ?? '/',
      pageCount: sectionPages.length,
      pages: sectionPages
        .sort((left, right) => left.relativePath.localeCompare(right.relativePath, 'zh-CN'))
        .map((page) => ({
          title: page.title,
          summary: page.summary,
          url: page.url,
          relativePath: page.relativePath,
          sourcePath: page.sourcePath,
          wordCount: page.wordCount,
          headings: page.headings,
        })),
    }))
    .filter((section) => section.pageCount > 0)
}

function getSectionKey(relativePath) {
  const firstSegment = relativePath.split('/')[0]
  return sectionTitleMap.has(firstSegment) ? firstSegment : 'root'
}

function toDocumentUrl(relativePath) {
  const normalizedRelativePath = relativePath.replace(/\.md$/iu, '')
  if (normalizedRelativePath === 'index') {
    return '/'
  }

  return `/${normalizedRelativePath}`
}

function fallbackTitleFromPath(relativePath) {
  if (relativePath === 'index.md') {
    return 'ColorVision 文档首页'
  }

  const fileName = relativePath.split('/').pop()?.replace(/\.md$/iu, '') || 'Untitled'
  if (fileName.toLowerCase() === 'readme') {
    const parentSegment = relativePath.split('/').slice(-2, -1)[0]
    return formatTitle(parentSegment || 'README')
  }

  return formatTitle(fileName)
}

function formatTitle(value) {
  return value
    .replace(/[-_]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

function normalizeMarkdownLine(line) {
  let text = line

  if (/^[\s|:-]+$/u.test(text)) {
    return ''
  }

  text = text.replace(/<!--.*?-->/gu, ' ')
  text = text.replace(/^>\s?/u, '')
  text = text.replace(/^[-*+]\s+/u, '')
  text = text.replace(/^\d+\.\s+/u, '')
  text = text.replace(/\|/g, ' ')
  text = text.replace(/!\[([^\]]*)\]\([^)]*\)/gu, '$1')
  text = text.replace(/\[([^\]]+)\]\([^)]*\)/gu, '$1')
  text = text.replace(/`([^`]+)`/gu, '$1')
  text = text.replace(/<[^>]+>/gu, ' ')
  text = text.replace(/[*_~#]+/g, ' ')

  return normalizeWhitespace(text)
}

function normalizeInlineText(text) {
  return normalizeWhitespace(
    text
      .replace(/`([^`]+)`/gu, '$1')
      .replace(/\[([^\]]+)\]\([^)]*\)/gu, '$1')
      .replace(/<[^>]+>/gu, ' '),
  )
}

function normalizeWhitespace(text) {
  return text.replace(/\s+/g, ' ').trim()
}

function createSummary(text, maxLength = 220) {
  const normalizedText = normalizeWhitespace(text || '')
  if (!normalizedText) {
    return ''
  }

  if (normalizedText.length <= maxLength) {
    return normalizedText
  }

  return `${normalizedText.slice(0, maxLength).trimEnd()}...`
}

function countWords(text) {
  if (!text) {
    return 0
  }

  return text.split(/\s+/).filter(Boolean).length
}

function createSlug(text, slugCounts) {
  const baseSlug = (text || '')
    .toLowerCase()
    .replace(/[\t\n\r]+/g, ' ')
    .replace(/[!"#$%&'()*+,./:;<=>?@[\\\]^`{|}~]+/g, '')
    .trim()
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-')

  const fallbackSlug = baseSlug || 'section'
  const count = slugCounts.get(fallbackSlug) ?? 0
  slugCounts.set(fallbackSlug, count + 1)

  return count === 0 ? fallbackSlug : `${fallbackSlug}-${count}`
}

function normalizePath(filePath) {
  return filePath.split(path.sep).join('/')
}

main().catch((error) => {
  console.error('Failed to generate docs index artifacts.')
  console.error(error)
  process.exitCode = 1
})