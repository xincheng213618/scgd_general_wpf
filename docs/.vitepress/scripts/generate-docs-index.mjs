import fs from 'node:fs/promises'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { generateKnowledge, parseFrontmatter } from './knowledge.mjs'
import {
  defaultLocaleKey,
  getLocaleDefinition,
  getLocaleHomeUrl,
  getSectionSortIndex,
  getSectionTitle,
  getSectionUrl,
  isLocaleKey,
  localeOrder,
} from '../i18n/locales.mjs'

const docsRoot = path.resolve(process.cwd(), 'docs')
const distRoot = path.join(docsRoot, '.vitepress', 'dist')
const manifestOutputPath = path.join(distRoot, 'docs-manifest.json')
const searchIndexOutputPath = path.join(distRoot, 'docs-search-index.json')

async function main() {
  await ensureDistDirectory()

  const catalog = await generateKnowledge(process.cwd(), true)
  const pages = []
  const searchEntries = []

  for (const knowledge of catalog.entries) {
    const htmlRelativePath = knowledge.url.endsWith('/') ? `${knowledge.url.slice(1)}index.html` : `${knowledge.url.slice(1)}.html`
    const builtHtml = await fs.readFile(path.join(distRoot, htmlRelativePath), 'utf8')
    const page = await buildPageRecord(path.resolve(process.cwd(), knowledge.source), knowledge, builtHtml)
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
    locales: localeOrder.map((localeKey) => ({
      key: localeKey,
      label: getLocaleDefinition(localeKey).label,
      url: getLocaleHomeUrl(localeKey),
    })),
    pagesCount: pages.length,
    entriesCount: searchEntries.length,
    sections,
    pages: pages.map((page) => ({
      ...knowledgeProjection(page),
      localeKey: page.localeKey,
      localeLabel: page.localeLabel,
      title: page.title,
      summary: page.summary,
      url: page.url,
      relativePath: page.relativePath,
      contentRelativePath: page.contentRelativePath,
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
    locales: localeOrder.map((localeKey) => ({
      key: localeKey,
      label: getLocaleDefinition(localeKey).label,
      url: getLocaleHomeUrl(localeKey),
    })),
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

export async function buildPageRecord(markdownFilePath, knowledge, builtHtml) {
  const rawContent = await fs.readFile(markdownFilePath, 'utf8')
  const relativePath = knowledge.source.replace(/^docs\//u, '')
  const localeKey = getLocaleKey(relativePath)
  const localeLabel = getLocaleDefinition(localeKey).label
  const contentRelativePath = stripLocalePrefix(relativePath, localeKey)
  const sourcePath = knowledge.source
  const url = knowledge.url
  const sectionKey = knowledge.domain
  const sectionTitle = getSectionTitle(localeKey, sectionKey)
  const parsedMarkdown = parseMarkdown(rawContent)
  bindBuiltHeadings(parsedMarkdown, builtHtml, knowledge.source)
  const title = knowledge.title
  const summary = knowledge.summary
  const headings = parsedMarkdown.headings.map((heading) => ({
    depth: heading.depth,
    text: heading.text,
    slug: heading.slug,
    url: heading.depth === 1 ? url : `${url}#${encodeURIComponent(heading.slug)}`,
  }))

  return {
    knowledge,
    localeKey,
    localeLabel,
    title,
    summary,
    url,
    relativePath,
    contentRelativePath,
    sourcePath,
    sectionKey,
    sectionTitle,
    searchable: knowledge.searchable,
    wordCount: countWords(parsedMarkdown.plainText),
    headings,
    sections: parsedMarkdown.sections.map((section) => ({
      title: section.title,
      slug: section.slug,
      depth: section.depth,
      titles: section.titles,
      text: section.text,
      summary: createSummary(section.text),
      url: section.slug ? `${url}#${encodeURIComponent(section.slug)}` : url,
    })),
  }
}

function parseMarkdown(markdownContent) {
  const contentWithoutFrontmatter = parseFrontmatter(markdownContent).body
  const lines = contentWithoutFrontmatter.split(/\r?\n/)
  const headings = []
  const sections = []
  const headingStack = []
  const summaryLines = []

  let pageTitle = ''
  let currentSection = createSection('', '', 1, [])
  let codeFence = null

  for (const rawLine of lines) {
    const trimmedLine = rawLine.trim()

    const fenceMarker = /^(`{3,}|~{3,})/u.exec(trimmedLine)?.[1]
    if (fenceMarker && (!codeFence || (fenceMarker[0] === codeFence[0] && fenceMarker.length >= codeFence.length))) {
      codeFence = codeFence ? null : fenceMarker
      continue
    }

    if (!codeFence) {
      const headingMatch = /^(#{1,6})\s+(.*)$/u.exec(trimmedLine)
      if (headingMatch) {
        flushSection(sections, currentSection)

        const depth = headingMatch[1].length
        const headingText = normalizeInlineText(headingMatch[2].replace(/\s+\{[^}]*\}\s*$/u, ''))
        const slug = `heading-${headings.length}`

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

    const normalizedLine = codeFence ? normalizeWhitespace(rawLine) : normalizeMarkdownLine(rawLine)
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

export function buildSearchEntries(page) {
  if (!page.searchable) {
    return []
  }

  const entries = []

  entries.push({
    ...knowledgeProjection(page),
    id: page.url,
    kind: 'page',
    localeKey: page.localeKey,
    localeLabel: page.localeLabel,
    sectionKey: page.sectionKey,
    sectionTitle: page.sectionTitle,
    title: page.title,
    titles: [page.title],
    text: page.summary,
    url: page.url,
    relativePath: page.relativePath,
  })

  for (const section of page.sections) {
    if (!section.slug) {
      continue
    }

    entries.push({
      ...knowledgeProjection(page),
      id: section.url,
      kind: 'section',
      localeKey: page.localeKey,
      localeLabel: page.localeLabel,
      sectionKey: page.sectionKey,
      sectionTitle: page.sectionTitle,
      title: formatSectionSearchTitle(page, section),
      titles: section.titles.length > 0 ? section.titles : [page.title],
      text: section.text,
      summary: section.summary,
      url: section.url,
      relativePath: page.relativePath,
    })
  }

  return entries
}

function formatSectionSearchTitle(page, section) {
  if (!section.title || section.title === page.title) {
    return page.title
  }

  return `${page.title}：${section.title}`
}

function knowledgeProjection(page) {
  const { knowledge_id, knowledge_type, status, aliases, code_paths, test_paths, related } = page.knowledge
  return { knowledge_id, knowledge_type, status, aliases, code_paths, test_paths, related, sourcePath: page.sourcePath }
}

function buildSections(pages) {
  const pagesBySection = new Map()

  for (const page of pages) {
    const compositeKey = `${page.localeKey}:${page.sectionKey}`

    if (!pagesBySection.has(compositeKey)) {
      pagesBySection.set(compositeKey, [])
    }

    pagesBySection.get(compositeKey).push(page)
  }

  return [...pagesBySection.entries()]
    .map(([key, sectionPages]) => {
      const [localeKey, sectionKey] = key.split(':')
      return {
        key,
        localeKey,
        localeLabel: getLocaleDefinition(localeKey).label,
        sectionKey,
        title: getSectionTitle(localeKey, sectionKey),
        url: getSectionUrl(localeKey, sectionKey) ?? sectionPages[0]?.url ?? getLocaleHomeUrl(localeKey),
        pageCount: sectionPages.length,
        pages: sectionPages
          .sort((left, right) => left.relativePath.localeCompare(right.relativePath, 'zh-CN'))
          .map((page) => ({
            ...knowledgeProjection(page),
            localeKey: page.localeKey,
            localeLabel: page.localeLabel,
            title: page.title,
            summary: page.summary,
            url: page.url,
            relativePath: page.relativePath,
            contentRelativePath: page.contentRelativePath,
            sourcePath: page.sourcePath,
            wordCount: page.wordCount,
            headings: page.headings,
          })),
      }
    })
    .sort(compareSectionGroup)
    .filter((section) => section.pageCount > 0)
}

function compareSectionGroup(left, right) {
  const localeOrderDelta = localeOrder.indexOf(left.localeKey) - localeOrder.indexOf(right.localeKey)
  if (localeOrderDelta !== 0) {
    return localeOrderDelta
  }

  return getSectionSortIndex(left.localeKey, left.sectionKey) - getSectionSortIndex(right.localeKey, right.sectionKey)
}

function getLocaleKey(relativePath) {
  const firstSegment = relativePath.split('/')[0]
  return isLocaleKey(firstSegment) ? firstSegment : defaultLocaleKey
}

function stripLocalePrefix(relativePath, localeKey) {
  if (localeKey === defaultLocaleKey) {
    return relativePath
  }

  return relativePath.slice(localeKey.length + 1)
}

export function normalizeMarkdownLine(line) {
  let text = line
  const codeSpans = []
  text = text.replace(/`([^`]+)`/gu, (_, code) => `CODE_SPAN_${codeSpans.push(code) - 1}_TOKEN`)

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
  text = text.replace(/<[^>]+>/gu, ' ')
  text = text.replace(/\*\*([^*]+)\*\*/gu, '$1').replace(/~~([^~]+)~~/gu, '$1')
  text = text.replace(/CODE_SPAN_(\d+)_TOKEN/gu, (_, index) => codeSpans[Number(index)])

  return normalizeWhitespace(text)
}

function normalizeInlineText(text) {
  return normalizeMarkdownLine(text)
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

function decodeHtml(value) {
  const named = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', ZeroWidthSpace: '' }
  return value.replace(/&(#x[\da-f]+|#\d+|amp|lt|gt|quot|apos|nbsp|ZeroWidthSpace);/giu, (entity, name) => {
    if (name.startsWith('#')) return String.fromCodePoint(Number.parseInt(name.slice(name[1].toLowerCase() === 'x' ? 2 : 1), name[1].toLowerCase() === 'x' ? 16 : 10))
    return named[name] ?? entity
  })
}

export function readHeadingAnchors(html) {
  if (typeof html !== 'string') throw new Error('Built HTML is required; section anchors must never be guessed')
  const headings = []
  for (const match of html.matchAll(/<h([1-6])\b([^>]*)>([\s\S]*?)<\/h\1>/giu)) {
    const id = /(?:^|\s)id=(?:"([^"]*)"|'([^']*)')/iu.exec(match[2])
    if (!id) continue
    const content = match[3].replace(/<a\b[^>]*\bclass="[^"]*\bheader-anchor\b[^"]*"[^>]*>[\s\S]*?<\/a>/giu, '').replace(/<[^>]*>/gu, '')
    headings.push({ depth: Number(match[1]), slug: decodeHtml(id[1] ?? id[2]), text: normalizeWhitespace(decodeHtml(content)) })
  }
  return headings
}

function bindBuiltHeadings(parsed, html, source) {
  // VitePress 1.6 uses NFKD, punctuation replacement and markdown-it-anchor's
  // duplicate/custom-id rules. The built HTML is authoritative across versions.
  const actual = readHeadingAnchors(html)
  if (parsed.headings.length !== actual.length) throw new Error(`${source}: ${parsed.headings.length} Markdown headings but ${actual.length} built anchors; rebuild or inspect the heading parser`)
  const headingMap = new Map()
  parsed.headings.forEach((heading, index) => {
    if (heading.depth !== actual[index].depth) throw new Error(`${source}: built heading order/depth differs at ${heading.text}`)
    headingMap.set(heading.slug, actual[index])
    Object.assign(heading, actual[index])
  })
  for (const section of parsed.sections) {
    if (!section.slug) continue
    const heading = headingMap.get(section.slug)
    section.slug = heading.slug
    section.title = heading.text
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error('Failed to generate docs index artifacts.')
    console.error(error)
    process.exitCode = 1
  })
}
