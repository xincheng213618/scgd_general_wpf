import fs from 'node:fs/promises'
import path from 'node:path'
import { ErrorCodes, NodeTypes, parse } from '@vue/compiler-dom'

// This is a website-only check. The knowledge CLI and its lightweight tests do
// not import the HTML parser or require the website dependencies.
export function parseHtmlLinks(html) {
  if (html.includes('\0')) throw new Error('invalid NUL in built HTML')
  const tree = parse(html, {
    parseMode: 'html', comments: false, prefixIdentifiers: false,
    whitespace: 'preserve', delimiters: ['\0', '\0'],
    // Repeated attributes are legal to recover from: browsers use the first.
    onError(error) { if (error.code !== ErrorCodes.DUPLICATE_ATTRIBUTE) throw error },
  })
  const ids = new Set()
  const hrefs = []
  function visit(node) {
    if (node.type === NodeTypes.ELEMENT) {
      const attributes = new Map()
      for (const attribute of node.props) {
        if (attribute.type !== NodeTypes.ATTRIBUTE) continue
        const name = attribute.name.toLowerCase()
        if (!attributes.has(name)) attributes.set(name, attribute.value?.content ?? '')
      }
      if (attributes.has('id')) ids.add(attributes.get('id'))
      const tag = node.tag.toLowerCase()
      if (tag === 'a' && attributes.has('name')) ids.add(attributes.get('name'))
      if (['a', 'area'].includes(tag) && attributes.has('href')) hrefs.push(attributes.get('href'))
      // Template contents are inert, whereas real elements inside pre/code are
      // still DOM nodes. Escaped code examples are already plain text in the AST.
      if (['template', 'script', 'style', 'textarea', 'title', 'noscript', 'iframe', 'xmp', 'noembed', 'noframes'].includes(tag)) return
    }
    for (const child of node.children ?? []) visit(child)
  }
  visit(tree)
  return { ids, hrefs }
}

export function hasHtmlAnchor(html, fragment) {
  let requested
  try { requested = decodeURIComponent(fragment) } catch { return false }
  return parseHtmlLinks(html).ids.has(requested)
}

function staysInside(root, target) {
  const relative = path.relative(root, target)
  return relative !== '..' && !relative.startsWith(`..${path.sep}`) && !path.isAbsolute(relative)
}

async function collectHtmlFiles(directory) {
  const files = []
  for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
    const target = path.join(directory, entry.name)
    // Do not follow directory symlinks. Link targets are separately realpath-
    // checked before reading, including paths traversing a junction on Windows.
    if (entry.isDirectory()) files.push(...await collectHtmlFiles(target))
    else if (entry.isFile() && /\.html?$/iu.test(entry.name)) files.push(target)
  }
  return files.sort()
}

function resolveLocalFragment(source, href, basePath) {
  const target = href.trim()
  if (/^(?:[a-z][a-z\d+.-]*:|\/\/)/iu.test(target)) return null
  const hash = target.indexOf('#')
  if (hash < 0 || hash === target.length - 1) return null
  const beforeHash = target.slice(0, hash)
  const rawPath = beforeHash.split('?')[0]
  const rawFragment = target.slice(hash + 1).split(':~:')[0]
  let pathname
  let fragment
  try {
    pathname = decodeURIComponent(rawPath)
    fragment = decodeURIComponent(rawFragment)
  } catch { throw new Error('invalid percent encoding') }
  if (/[\\\0]/u.test(pathname)) throw new Error('invalid path separator or NUL')

  let relative
  if (!pathname) relative = source
  else if (pathname.startsWith('/')) {
    const base = basePath.replace(/\/$/u, '')
    // These are rendered hrefs, not Markdown source routes. With a non-root
    // deployment base, /topic navigates outside the deployed site even when
    // dist/topic.html happens to exist.
    if (base && pathname !== base && !pathname.startsWith(`${base}/`)) throw new Error(`link outside site base ${basePath}`)
    relative = pathname.slice(base.length).replace(/^\//u, '')
  } else relative = path.posix.join(path.posix.dirname(source), pathname)
  relative = path.posix.normalize(relative || '.')
  if (relative === '..' || relative.startsWith('../') || relative.startsWith('/')) throw new Error('link escapes built output')

  const extension = path.posix.extname(relative).toLowerCase()
  // A dot does not imply an asset: ColorVision.Common is also a valid clean
  // page URL. Prefer an existing literal file, then the site's HTML routes.
  const directoryRoute = relative === '.' || pathname.endsWith('/') || /(?:^|\/)\.{1,2}$/u.test(pathname)
  const candidates = directoryRoute ? [path.posix.join(relative, 'index.html')]
    : extension === '.html' || extension === '.htm' ? [relative]
      : [relative, `${relative}.html`, path.posix.join(relative, 'index.html')]
  // Split text directives before percent-decoding: %3A~%3A remains part of a
  // literal element ID. A preceding element fragment is still validated.
  return { candidates, fragment }
}

export async function validateBuiltLinkFragments(distRoot, { basePath = '/scgd_general_wpf/' } = {}) {
  if (!basePath.startsWith('/') || !basePath.endsWith('/')) throw new Error('invalid site basePath')
  const root = await fs.realpath(distRoot)
  const files = await collectHtmlFiles(root)
  const parsed = new Map()
  const failures = []
  let checkedLinks = 0
  async function readPage(file) {
    if (!parsed.has(file)) parsed.set(file, parseHtmlLinks(await fs.readFile(file, 'utf8')))
    return parsed.get(file)
  }
  for (const file of files) {
    const source = path.relative(root, file).split(path.sep).join('/')
    let page
    try { page = await readPage(file) } catch (error) {
      failures.push(`${source}: invalid built HTML: ${error.message}`)
      continue
    }
    for (const href of new Set(page.hrefs)) {
      try {
        const link = resolveLocalFragment(source, href, basePath)
        if (!link) continue
        checkedLinks += 1
        let destination
        for (const candidate of link.candidates) {
          const absolute = path.resolve(root, candidate)
          if (!staysInside(root, absolute)) throw new Error('link escapes built output')
          let actual
          try { actual = await fs.realpath(absolute) } catch (error) {
            if (['ENOENT', 'ENOTDIR'].includes(error.code)) continue
            throw error
          }
          if (!staysInside(root, actual)) throw new Error('link escapes built output through symlink')
          if ((await fs.stat(actual)).isFile()) { destination = actual; break }
        }
        if (!destination) throw new Error(`missing target (${link.candidates.join(' or ')})`)
        // PDF/SVG/media fragments have their own meaning; only check that the
        // local asset exists. No external URLs are requested by this validator.
        if (!/\.html?$/iu.test(destination) || !link.fragment) continue
        const ids = (await readPage(destination)).ids
        if (!ids.has(link.fragment) && link.fragment.toLowerCase() !== 'top') {
          throw new Error(`missing anchor #${link.fragment} in ${path.relative(root, destination)}`)
        }
      } catch (error) { failures.push(`${source}: ${JSON.stringify(href)}: ${error.message}`) }
    }
  }
  return { htmlFiles: files.length, checkedLinks, failures }
}
