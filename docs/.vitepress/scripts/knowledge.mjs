import fs from 'node:fs/promises'
import path from 'node:path'
import { createHash } from 'node:crypto'
import { fileURLToPath } from 'node:url'

export const knowledgeFields = ['knowledge_id', 'knowledge_type', 'status', 'summary', 'aliases', 'code_paths', 'test_paths', 'related']
const arrayFields = new Set(['aliases', 'code_paths', 'test_paths', 'related'])
const generatedPaths = ['docs/knowledge/index.md', 'docs/knowledge/catalog.json', 'docs/.vitepress/i18n/navigation-data.json']
const generatedMapDirectories = ['docs/knowledge/domains', 'docs/knowledge/code']
const skippedDirectories = new Set(['.vitepress', 'node_modules', 'en', 'zh-tw', 'ja', 'ko'])
export const domainDefinitions = [
  ['governance', 'AI 共治与知识维护', '工作边界、知识维护、文档与源码冲突、检索验收。'],
  ['platform', '平台与架构', '宿主架构、模块责任、扩展分流与权限边界。'],
  ['ui', 'UI 与图像交互', '属性编辑器、窗口组件、图像交互和绘制扩展。'],
  ['engine', '设备、服务与结果', '设备服务、MQTT、模板宿主和结果展示。'],
  ['flow', '流程编排与执行', '流程编辑、节点运行、参数传递与完成语义。'],
  ['algorithms', '算法与模板', '算法平台、传统模板、计算适配和规划中的能力。'],
  ['copilot', 'Copilot', 'Agent会话、工具契约、上下文、恢复和MCP边界。'],
  ['projects', '客户项目', '客户包、业务流程、协议对接与结果留存。'],
  ['plugins', '插件与扩展', '插件发现、生命周期、已有插件和集成边界。'],
  ['delivery', '构建、测试与交付', '克隆环境、构建依赖、测试、发布脚本和更新。'],
  ['operations', '运行与现场排查', '安装使用、设备配置、现场故障、日志和数据管理。'],
].map(([key, title, summary]) => ({ key, title, summary }))
// Presentation order only: topic membership and module names come from real
// code_paths, never from a second manually maintained topic/module table.
const sourceRootOrder = ['ColorVision', 'UI', 'Engine', 'Native', 'Plugins', 'Projects', 'Web', 'Scripts', 'Test']
const statusLabels = { current: '当前', planned: '规划', historical: '历史' }
const typeOrder = ['index', 'topic', 'guide', 'reference', 'decision']

// This is intentionally NOT a YAML parser. Only declared one-line knowledge
// fields and page flags are read; unrelated VitePress YAML is left untouched.
export function parseFrontmatter(markdown, source = '<markdown>') {
  const normalized = markdown.replace(/^\uFEFF/u, '').replace(/\r\n/gu, '\n')
  const lines = normalized.split('\n')
  if (lines[0]?.trimEnd() !== '---') return { metadata: {}, body: normalized, redirect: false, searchable: true }
  const end = lines.findIndex((line, index) => index > 0 && line.trimEnd() === '---')
  if (end < 0) throw new Error(`${source}: unterminated frontmatter`)
  const metadata = {}
  const seen = new Set()
  let redirect = false
  let searchable = true
  let previousKnowledgeField = null
  for (let index = 1; index < end; index += 1) {
    const line = lines[index]
    if (!line.trim() || line.trimStart().startsWith('#')) continue
    const match = /^([A-Za-z_][\w-]*):(?:[ \t]*(.*))?$/u.exec(line)
    if (!match) {
      if (previousKnowledgeField && /^\s+/u.test(line)) throw new Error(`${source}:${index + 1}: ${previousKnowledgeField} must be a single-line field`)
      continue
    }
    const [, key, raw = ''] = match
    previousKnowledgeField = knowledgeFields.includes(key) ? key : null
    if (!knowledgeFields.includes(key) && !['redirect_from_deleted_page', 'search'].includes(key)) {
      if (key.startsWith('knowledge_')) throw new Error(`${source}:${index + 1}: unknown knowledge field ${key}`)
      continue
    }
    if (seen.has(key)) throw new Error(`${source}:${index + 1}: duplicate field ${key}`)
    seen.add(key)
    if (key === 'redirect_from_deleted_page' || key === 'search') {
      if (!['true', 'false'].includes(raw.trim())) throw new Error(`${source}:${index + 1}: ${key} must be true or false`)
      if (key === 'redirect_from_deleted_page') redirect = raw.trim() === 'true'
      else searchable = raw.trim() !== 'false'
      continue
    }
    const value = raw.trim()
    if (arrayFields.has(key)) {
      let parsed
      try { parsed = JSON.parse(value) } catch { throw new Error(`${source}:${index + 1}: ${key} must be a one-line JSON array of strings`) }
      if (!Array.isArray(parsed) || parsed.some((item) => typeof item !== 'string' || !item.trim() || item !== item.trim() || /[\u0000-\u001f]/u.test(item))) {
        throw new Error(`${source}:${index + 1}: ${key} must contain nonempty, trimmed strings`)
      }
      if (new Set(parsed).size !== parsed.length) throw new Error(`${source}:${index + 1}: duplicate value in ${key}`)
      metadata[key] = parsed
    } else {
      let parsed = value
      if (value.startsWith('"')) {
        try { parsed = JSON.parse(value) } catch { throw new Error(`${source}:${index + 1}: invalid JSON string for ${key}`) }
      } else if (!value || /^[\[\]{}'|>&*!]/u.test(value) || /\s#|:\s/u.test(value)) {
        throw new Error(`${source}:${index + 1}: ${key} must be a plain scalar or JSON-quoted string`)
      }
      if (typeof parsed !== 'string' || !parsed.trim() || /[\u0000-\u001f]/u.test(parsed)) throw new Error(`${source}:${index + 1}: invalid string for ${key}`)
      metadata[key] = parsed
    }
  }
  return { metadata, body: lines.slice(end + 1).join('\n'), redirect, searchDisabled: !searchable, searchable: searchable && !redirect }
}

export function validateRelativePath(value, source = 'path') {
  const normalized = typeof value === 'string' && value.endsWith('/') ? value.slice(0, -1) : value
  if (typeof normalized !== 'string' || !normalized || normalized !== normalized.trim() || /[\\:*?"<>|\u0000-\u001f]/u.test(normalized)
    || normalized.startsWith('/') || normalized.split('/').some((part) => ['', '.', '..'].includes(part))) {
    throw new Error(`${source}: use a concrete repository-relative path with / separators, not a URL, glob, or traversal: ${value}`)
  }
  return normalized
}

function isInside(root, target) {
  const relative = path.relative(root, target)
  return relative !== '' && !relative.startsWith(`..${path.sep}`) && relative !== '..' && !path.isAbsolute(relative)
}

export async function validateRepositoryPath(repoRoot, value, source = 'path', directoryCache = new Map()) {
  const normalized = validateRelativePath(value, source)
  const absolute = path.resolve(repoRoot, value)
  if (!isInside(path.resolve(repoRoot), absolute)) throw new Error(`${source}: path escapes repository: ${value}`)
  let actual
  try { actual = await fs.realpath(absolute) } catch { throw new Error(`${source}: mapped path does not exist: ${value}`) }
  if (!isInside(await fs.realpath(repoRoot), actual)) throw new Error(`${source}: resolved path escapes repository: ${value}`)
  const stat = await fs.stat(actual)
  if (!stat.isFile() && !stat.isDirectory()) throw new Error(`${source}: path must be a file or directory: ${value}`)
  // Windows resolves wrong-case paths, but the same checkout is validated on
  // Linux in CI. Check spelling here so local success is portable.
  let parent = path.resolve(repoRoot)
  for (const segment of normalized.split('/')) {
    if (!directoryCache.has(parent)) directoryCache.set(parent, new Set(await fs.readdir(parent)))
    if (!directoryCache.get(parent).has(segment)) throw new Error(`${source}: path casing differs from the repository: ${value}`)
    parent = path.join(parent, segment)
  }
  return { isDirectory: stat.isDirectory() }
}

export async function collectKnowledgeMarkdown(repoRoot) {
  const docsRoot = path.join(repoRoot, 'docs')
  const files = []
  async function walk(directory) {
    for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
      if (entry.name.startsWith('.') || entry.name.startsWith('_') || entry.name.startsWith('~')) continue
      const fullPath = path.join(directory, entry.name)
      if (entry.isDirectory()) {
        if (!skippedDirectories.has(entry.name)) await walk(fullPath)
      } else if (entry.isFile() && entry.name.endsWith('.md') && entry.name.toLowerCase() !== 'agents.md') {
        const source = path.relative(repoRoot, fullPath).replace(/\\/gu, '/')
        if (!generatedPaths.includes(source) && !generatedMapDirectories.some((directory) => source.startsWith(`${directory}/`))) files.push(source)
      }
    }
  }
  await walk(docsRoot)
  return files.sort()
}

export function documentUrl(source) {
  const stem = source.replace(/^docs\//u, '').replace(/\.md$/u, '')
  if (stem === 'index') return '/'
  if (stem.endsWith('/index')) return `/${stem.slice(0, -5)}`
  return `/${stem}`
}

function titleFromBody(body, source) {
  let fence = null
  for (const line of body.split('\n')) {
    const marker = /^\s*(`{3,}|~{3,})/u.exec(line)?.[1]
    if (marker) { if (!fence) fence = marker[0]; else if (marker[0] === fence) fence = null; continue }
    if (!fence && /^#\s+/u.test(line)) return line.replace(/^#\s+/u, '').replace(/\s+#+\s*$/u, '').trim()
  }
  return source === 'docs/index.md' ? 'ColorVision 项目知识入口' : path.posix.basename(source, '.md')
}

export async function buildCatalog(repoRoot) {
  const entries = []
  const errors = []
  const ids = new Set()
  const directoryCache = new Map()
  const validatedPaths = new Map()
  for (const source of await collectKnowledgeMarkdown(repoRoot)) {
    try {
      const raw = await fs.readFile(path.join(repoRoot, source), 'utf8')
      const parsed = parseFrontmatter(raw, source)
      if (parsed.redirect) {
        if (!parsed.searchDisabled) throw new Error(`${source}: redirect must set search: false`)
        continue
      }
      const metadata = parsed.metadata
      for (const field of knowledgeFields) if (!(field in metadata)) throw new Error(`${source}: missing ${field}`)
      if (!/^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$/u.test(metadata.knowledge_id)) throw new Error(`${source}: invalid knowledge_id`)
      if (ids.has(metadata.knowledge_id)) throw new Error(`${source}: duplicate knowledge_id ${metadata.knowledge_id}`)
      if (!typeOrder.includes(metadata.knowledge_type)) throw new Error(`${source}: invalid knowledge_type ${metadata.knowledge_type}`)
      if (!Object.hasOwn(statusLabels, metadata.status)) throw new Error(`${source}: invalid status ${metadata.status}`)
      const domain = metadata.knowledge_id.split('.')[0]
      if (!domainDefinitions.some((item) => item.key === domain)) throw new Error(`${source}: unknown knowledge domain ${domain}`)
      for (const field of ['code_paths', 'test_paths']) {
        for (const mappedPath of metadata[field]) {
          if (!validatedPaths.has(mappedPath)) {
            validatedPaths.set(mappedPath, await validateRepositoryPath(repoRoot, mappedPath, `${source}:${field}`, directoryCache))
          }
        }
        metadata[field] = metadata[field].map((mappedPath) => validateRelativePath(mappedPath))
        if (new Set(metadata[field]).size !== metadata[field].length) throw new Error(`${source}: duplicate normalized path in ${field}`)
      }
      if (metadata.related.includes(metadata.knowledge_id)) throw new Error(`${source}: related cannot reference itself`)
      ids.add(metadata.knowledge_id)
      const codeScopes = [...new Set(metadata.code_paths.map((mappedPath) => {
        const parts = mappedPath.split('/')
        const info = validatedPaths.get(mappedPath) ?? validatedPaths.get(`${mappedPath}/`)
        return parts.slice(0, Math.min(2, parts.length - (info.isDirectory ? 0 : 1))).join('/') || '.'
      }))].sort()
      entries.push({ ...metadata, code_scopes: codeScopes, domain, title: titleFromBody(parsed.body, source), source, url: documentUrl(source), searchable: parsed.searchable,
        source_hash: createHash('sha256').update(raw.replace(/\r\n/gu, '\n')).digest('hex') })
    } catch (error) { errors.push(error.message) }
  }
  for (const entry of entries) {
    for (const related of entry.related) if (!ids.has(related)) errors.push(`${entry.source}: unknown related knowledge_id ${related}`)
    if (entry.knowledge_type !== 'index' && !entry.related.length && !entries.some((other) => other.related.includes(entry.knowledge_id))) {
      errors.push(`${entry.source}: isolated knowledge; connect related to a domain entry or another topic`)
    }
  }
  if (errors.length) throw new Error(errors.join('\n'))
  entries.sort((left, right) => left.knowledge_id.localeCompare(right.knowledge_id, 'en'))
  return { schema_version: 2, language: 'zh-CN', source_of_truth: 'Markdown frontmatter and body; verify implementation against mapped code and tests.', entries }
}

export function catalogGroups(catalog) {
  return allCatalogGroups(catalog).filter((group) => group.entries.length)
}

function allCatalogGroups(catalog) {
  return domainDefinitions.map((domain) => ({ ...domain,
    entries: catalog.entries.filter((entry) => entry.domain === domain.key).sort((a, b) => typeOrder.indexOf(a.knowledge_type) - typeOrder.indexOf(b.knowledge_type) || a.knowledge_id.localeCompare(b.knowledge_id, 'en')),
  }))
}

export function codeCatalogGroups(catalog) {
  const roots = new Map()
  for (const entry of catalog.entries) {
    if (!Array.isArray(entry.code_scopes)) throw new Error('Catalog has no derived code_scopes; regenerate knowledge from Markdown and real repository paths.')
    for (const scope of entry.code_scopes.length ? entry.code_scopes : [null]) {
      const first = scope?.split('/')[0]
      const root = !first || first === 'docs' || first.startsWith('.') ? '.' : first
      if (!roots.has(root)) roots.set(root, new Map())
      const modules = roots.get(root)
      if (!modules.has(scope)) modules.set(scope, new Map())
      modules.get(scope).set(entry.knowledge_id, entry)
    }
  }
  const rank = (root) => root === '.' ? sourceRootOrder.length + 1 : sourceRootOrder.includes(root) ? sourceRootOrder.indexOf(root) : sourceRootOrder.length
  return [...roots].sort(([left], [right]) => rank(left) - rank(right) || left.localeCompare(right, 'en')).map(([root, scopes]) => {
    const safeRoot = /^[A-Za-z0-9_-]+$/u.test(root) ? root : `${root.replace(/[^A-Za-z0-9_-]/gu, '-')}-${createHash('sha256').update(root).digest('hex').slice(0, 12)}`
    const key = root === '.' ? 'repository' : `source-${safeRoot}`
    const modules = [...scopes].sort(([left], [right]) => {
      const priority = (scope) => scope === root ? 0 : scope === null ? 2 : 1
      return priority(left) - priority(right) || (left ?? '').localeCompare(right ?? '', 'en')
    }).map(([scope, topics]) => ({
      scope, title: scope === null ? '未声明源码关联' : scope === '.' ? '仓库根文件' : scope === root ? `${root}/ 根目录与跨模块关联` : scope,
      anchor: `module-${Buffer.from(scope ?? '<unmapped>').toString('hex')}`,
      entries: [...topics.values()].sort((a, b) => typeOrder.indexOf(a.knowledge_type) - typeOrder.indexOf(b.knowledge_type) || a.knowledge_id.localeCompare(b.knowledge_id, 'en')),
    }))
    return { key, root, title: root === '.' ? '仓库与知识基础设施' : root, modules,
      entries: [...new Map(modules.flatMap((module) => module.entries.map((entry) => [entry.knowledge_id, entry]))).values()],
    }
  })
}

function label(entry) { return `${entry.title}${entry.status === 'current' ? '' : ` [${statusLabels[entry.status]}]`}` }
function escapeMarkdown(value) { return value.replace(/([\\`*_[\]<>])/gu, '\\$1').replace(/\|/gu, '\\|') }

export function renderKnowledgeIndex(catalog) {
  const lines = ['---', 'generated_knowledge_index: true', 'search: false', 'editLink: false', 'prev: false', 'next: false', '---', '', '# 项目知识地图', '',
    '> 由 Markdown 元数据生成。不要手工编辑；在仓库根目录运行 `node docs/.vitepress/scripts/knowledge.mjs generate`。', '',
    '从现有 `AGENTS.md` 读取工作约束，再按源码职责进入模块；索引只负责定位，修改前核对正文、关联源码及测试。`规划`、`历史`不是当前能力。', '',
    '离线检索：`node docs/.vitepress/scripts/knowledge.mjs search "问题或代码符号"`；反向映射：`node docs/.vitepress/scripts/knowledge.mjs impact "仓库相对路径"`。', '',
    `共 ${catalog.entries.length} 个主题；默认 CLI 搜索只返回 current，使用 \`--all\` 明确包含规划与历史。`, '',
    '## 按源码根与模块定位', '',
    '分组由主题的 `code_paths` 和真实目录派生；同一主题可关联多个模块，各组数量不能相加。关联不等于完整调用图；仅引用源码根的概览不会扩散到每个子模块。', '',
    '| 源码根 | 目录分组 | 关联主题 |', '| --- | ---: | ---: |']
  for (const group of codeCatalogGroups(catalog)) {
    lines.push(`| [${escapeMarkdown(group.title)}](./code/${group.key}.md) | ${group.modules.length} | ${group.entries.length} |`)
  }
  lines.push('', '## 按能力领域补充检索', '', '跨源码模块的问题仍可从能力领域进入；这不是另一套按读者身份编排的手册。', '')
  for (const group of allCatalogGroups(catalog)) {
    lines.push(`- [${group.title}](./domains/${group.key}.md) — ${group.entries.length} 个主题；${group.summary}`)
  }
  return `${lines.join('\n').trimEnd()}\n`
}

export function renderCodeIndex(group) {
  const lines = ['---', 'generated_knowledge_index: true', 'search: false', 'editLink: false', 'prev: false', 'next: false', '---', '', `# ${group.title} 源码知识`, '',
    '> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。', '',
    '返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。', '',
    '以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。', '']
  for (const module of group.modules) {
    lines.push(`## ${escapeMarkdown(module.title)} {#${module.anchor}}`, '')
    for (const entry of module.entries) {
      const relative = path.posix.relative('docs/knowledge/code', entry.source)
      lines.push(`- [${escapeMarkdown(label(entry))}](${relative}) — \`${entry.knowledge_id}\``, `  ${escapeMarkdown(entry.summary)}`, '')
    }
  }
  return `${lines.join('\n').trimEnd()}\n`
}

export function renderDomainIndex(group) {
  const lines = ['---', 'generated_knowledge_index: true', 'search: false', 'editLink: false', 'prev: false', 'next: false', '---', '', `# ${group.title}`, '',
    '> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。', '',
    `${group.summary} 返回[知识总入口](../index.md)。`, '',
    '只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。', '']
  for (const entry of group.entries) {
    const relative = path.posix.relative('docs/knowledge/domains', entry.source)
    lines.push(`- [${escapeMarkdown(label(entry))}](${relative}) — \`${entry.knowledge_id}\``, `  ${escapeMarkdown(entry.summary)}`, '')
  }
  if (!group.entries.length) lines.push('当前没有已登记主题。', '')
  return `${lines.join('\n').trimEnd()}\n`
}

export function createNavigationData(catalog) {
  const item = (text, link) => ({ text: { root: text }, link })
  const groups = catalogGroups(catalog)
  const codeGroups = codeCatalogGroups(catalog)
  return {
    generated: 'knowledge.mjs; edit Markdown metadata, not this file',
    navItems: [item('首页', '/'), item('知识地图', '/knowledge/'),
      { text: { root: '源码模块' }, items: codeGroups.map((group) => item(group.title, `/knowledge/code/${group.key}`)) },
      { text: { root: '能力领域' }, items: groups.map((group) => item(group.title, `/knowledge/domains/${group.key}`)) },
      item('GitHub', 'https://github.com/xincheng213618/scgd_general_wpf')],
    sidebarItems: [{ text: { root: '检索入口' }, collapsed: false, items: [item('项目知识入口', '/'), item('源码知识地图', '/knowledge/')] },
      ...codeGroups.map((group) => ({ text: { root: group.title }, link: `/knowledge/code/${group.key}`, collapsed: true,
        items: group.modules.map((module) => ({ text: { root: module.title }, link: `/knowledge/code/${group.key}#${module.anchor}`, collapsed: true,
          items: module.entries.filter((entry) => entry.url !== '/').map((entry) => item(label(entry), entry.url)),
        })),
      })),
      { text: { root: '能力领域（补充检索）' }, collapsed: true, items: groups.map((group) => item(group.title, `/knowledge/domains/${group.key}`)) }],
  }
}

export function generatedArtifacts(catalog) {
  return new Map([
    [generatedPaths[0], renderKnowledgeIndex(catalog)],
    [generatedPaths[1], `${JSON.stringify(catalog, null, 2)}\n`],
    [generatedPaths[2], `${JSON.stringify(createNavigationData(catalog), null, 2)}\n`],
    ...allCatalogGroups(catalog).map((group) => [`docs/knowledge/domains/${group.key}.md`, renderDomainIndex(group)]),
    ...codeCatalogGroups(catalog).map((group) => [`docs/knowledge/code/${group.key}.md`, renderCodeIndex(group)]),
  ])
}

export async function generateKnowledge(repoRoot, check = false) {
  const catalog = await buildCatalog(repoRoot)
  const artifacts = generatedArtifacts(catalog)
  const unexpected = []
  async function inspectMaps(relative) {
    let entries
    try { entries = await fs.readdir(path.join(repoRoot, relative), { withFileTypes: true }) } catch (error) { if (error.code === 'ENOENT') return; throw error }
    for (const entry of entries) {
      const source = `${relative}/${entry.name}`
      if (entry.isDirectory()) await inspectMaps(source)
      else if (entry.name.endsWith('.md') && !artifacts.has(source)) unexpected.push(source)
    }
  }
  for (const directory of generatedMapDirectories) await inspectMaps(directory)
  if (unexpected.length) throw new Error(`Unexpected generated knowledge maps:\n${unexpected.sort().join('\n')}\nReview and remove obsolete maps before regenerating; generation never deletes unknown content.`)
  const stale = []
  for (const [relative, expected] of artifacts) {
    const target = path.join(repoRoot, relative)
    if (check) {
      let actual = ''
      try { actual = (await fs.readFile(target, 'utf8')).replace(/\r\n/gu, '\n') } catch { /* reported as stale */ }
      if (actual !== expected) stale.push(relative)
    } else {
      await fs.mkdir(path.dirname(target), { recursive: true })
      await fs.writeFile(target, expected, 'utf8')
    }
  }
  if (stale.length) throw new Error(`Generated knowledge artifacts are stale or missing:\n${stale.join('\n')}\nRun node docs/.vitepress/scripts/knowledge.mjs generate and include the resulting files.`)
  return catalog
}

function searchSymbolPattern(symbol) {
  const escaped = symbol.replace(/[.*+?^${}()|[\]\\]/gu, '\\$&')
  return new RegExp(`(?:^|[^a-z0-9_])${escaped}(?![a-z0-9_])`, 'u')
}

function qualifiedSearchOwners(symbol) {
  if (symbol.includes('/')) {
    // A repository path may be prefixed by an absolute checkout path. Keep
    // concrete trailing paths/file names, not every generic directory name.
    const parts = symbol.split('/')
    return parts.slice(1).map((_, index) => parts.slice(index + 1).join('/')).filter(Boolean)
  }
  const lastSeparator = Math.max(symbol.lastIndexOf('.'), symbol.lastIndexOf('::'))
  const owner = symbol.slice(0, lastSeparator)
  const localOwner = owner.split(/\.|::/u).at(-1)
  // Namespace.Type.Member -> Namespace.Type / Type, never Member or Namespace.
  // Do not split underscores or use generic leaf names such as Save as owners.
  return [...new Set([owner, localOwner].filter(Boolean))]
}

export function searchCatalog(catalog, query, { all = false, limit = 12 } = {}) {
  const normalized = query.replace(/\\/gu, '/').toLocaleLowerCase().trim()
  if (!normalized) throw new Error('search requires a query')
  // Chinese questions often touch code symbols without spaces (e.g. Foo返回).
  // Extract symbols independently; do not split underscores or C++ qualifiers.
  const symbols = [...normalized.matchAll(/\.?[a-z0-9_]+(?:[:./+=-]+[a-z0-9_]+)*/gu)].map(([symbol]) => symbol)
  const tokens = [...new Set([...normalized.split(/\s+/u), ...symbols])]
  const fragments = [...normalized.matchAll(/[\p{Script=Han}]{2,}/gu)].flatMap(([run]) => Array.from({ length: run.length - 1 }, (_, index) => run.slice(index, index + 2)))
  const terms = [...new Set([...tokens, ...fragments])]
  const qualified = [...new Set(symbols)].filter((symbol) => /[a-z0-9_](?:\.|::|\/)[a-z0-9_]/u.test(symbol)).map((symbol) => ({
    full: searchSymbolPattern(symbol),
    owners: qualifiedSearchOwners(symbol).map(searchSymbolPattern),
  }))
  return catalog.entries.filter((entry) => entry.searchable !== false && (all || entry.status === 'current')).map((entry) => {
    const exact = [entry.knowledge_id, entry.title, ...entry.aliases].map((value) => value.toLocaleLowerCase())
    const description = [...exact, entry.summary].join('\n').toLocaleLowerCase()
    const fields = [description, entry.source, ...entry.code_paths, ...entry.test_paths].join('\n').toLocaleLowerCase()
    const exactMatch = Number(exact.includes(normalized))
    let fullMatches = 0
    let ownerMatches = 0
    let ownerSpecificity = 0
    let describedOwners = 0
    for (const symbol of qualified) {
      if (symbol.full.test(fields)) fullMatches++
      else {
        const ownerIndex = symbol.owners.findIndex((owner) => owner.test(fields))
        if (ownerIndex >= 0) {
          ownerMatches++
          ownerSpecificity += symbol.owners.length - ownerIndex
          if (symbol.owners.some((owner) => owner.test(description))) describedOwners++
        }
      }
    }
    let score = exactMatch * 100 + ownerMatches * 5
    for (const term of terms) if (fields.includes(term)) score += tokens.includes(term) ? 10 : 1
    return { entry, score, exactMatch, fullMatches, ownerMatches, ownerSpecificity, describedOwners }
  }).filter((result) => result.score > 0)
    // score is the lexical tie-break, not the final rank. Preserve this order
    // when consuming results: exact/qualified/owner evidence takes precedence.
    .sort((a, b) => b.exactMatch - a.exactMatch || b.fullMatches - a.fullMatches || b.ownerMatches - a.ownerMatches
      || b.ownerSpecificity - a.ownerSpecificity || b.describedOwners - a.describedOwners
      || b.score - a.score || a.entry.knowledge_id.localeCompare(b.entry.knowledge_id, 'en'))
    .slice(0, limit).map(({ entry, score, exactMatch, fullMatches, ownerMatches }) => ({
      ...entry, score,
      match_kind: exactMatch ? 'exact' : fullMatches ? 'qualified-symbol' : ownerMatches ? 'owner-fallback' : 'text',
    }))
}

export function impactCatalog(catalog, changedPath) {
  const query = validateRelativePath(changedPath.replace(/\\/gu, '/'), 'impact')
  const intersects = (mapped) => mapped === query || query.startsWith(`${mapped}/`) || mapped.startsWith(`${query}/`)
  return catalog.entries.map((entry) => ({ ...entry,
    matched_paths: [...entry.code_paths, ...entry.test_paths, entry.source].filter(intersects),
  })).filter((entry) => entry.matched_paths.length)
}

export function validateRetrievalCases(catalog, fixture) {
  if (!Array.isArray(fixture.cases) || !fixture.cases.length) throw new Error('retrieval-cases.json requires a nonempty cases array')
  const ids = new Set(catalog.entries.map((entry) => entry.knowledge_id))
  const results = []
  const errors = []
  for (const item of fixture.cases) {
    if (typeof item.query !== 'string' || !item.query.trim() || !Array.isArray(item.expected_any) || !item.expected_any.length
      || item.expected_any.some((id) => !ids.has(id)) || typeof item.include_planned !== 'boolean') {
      errors.push(`Invalid retrieval case: ${JSON.stringify(item)}`)
      continue
    }
    const actual = searchCatalog(catalog, item.query, { all: item.include_planned, limit: 5 }).map((entry) => entry.knowledge_id)
    if (!actual.some((id) => item.expected_any.includes(id))) errors.push(`Retrieval "${item.query}": expected one of [${item.expected_any.join(', ')}] in top 5, got [${actual.join(', ')}]`)
    results.push({ query: item.query, actual })
  }
  if (errors.length) throw new Error(errors.join('\n'))
  return results
}

async function main() {
  const [command, ...args] = process.argv.slice(2)
  const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..')
  if (command === 'generate' || command === 'check') {
    const catalog = await generateKnowledge(repoRoot, command === 'check')
    console.log(`${command === 'check' ? 'Validated' : 'Generated'} knowledge: ${catalog.entries.length} topics, ${catalogGroups(catalog).length} domains.`)
    if (command === 'check') {
      const cases = JSON.parse(await fs.readFile(path.join(repoRoot, 'docs/knowledge/retrieval-cases.json'), 'utf8'))
      const results = validateRetrievalCases(catalog, cases)
      console.log(`Validated ${results.length} fixed retrieval cases (top 5; lexical retrieval, not an LLM quality benchmark).`)
      const { validateReadmeDocsLinks } = await import('./readme-links.mjs')
      const readmeLinks = await validateReadmeDocsLinks(repoRoot)
      console.log(`Validated ${readmeLinks.links} README-to-docs links in ${readmeLinks.readmes} repository READMEs (page targets, not fragments).`)
    }
    return
  }
  if (command === 'search' || command === 'impact') {
    const catalog = JSON.parse(await fs.readFile(path.join(repoRoot, 'docs/knowledge/catalog.json'), 'utf8'))
    const value = args.filter((arg) => arg !== '--all').join(' ').trim()
    if (!value) throw new Error(`${command} requires ${command === 'search' ? 'a query' : 'a repository-relative path'}`)
    const matches = command === 'search' ? searchCatalog(catalog, value, { all: args.includes('--all') }) : impactCatalog(catalog, value)
    for (const entry of matches) {
      console.log(`[${entry.status}] ${entry.knowledge_id} — ${entry.title}\n  ${entry.source}\n  ${entry.summary}`)
      if (command === 'impact') console.log(`  mapped: ${entry.matched_paths.join(', ')}`)
      else console.log(`  match: ${entry.match_kind}`)
    }
    if (command === 'search') console.log('Search reads metadata. Read the selected topic for source/test mappings; owner-fallback does not verify the requested member.')
    console.log(`${matches.length} match(es). Index is a locator, not proof of current behavior; verify source and tests. Use check to detect stale metadata.`)
    return
  }
  throw new Error('Usage: node docs/.vitepress/scripts/knowledge.mjs generate|check|search "query" [--all]|impact "path"')
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch((error) => { console.error(error.message); process.exitCode = 1 })
}
