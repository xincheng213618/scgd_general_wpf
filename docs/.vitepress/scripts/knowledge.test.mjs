import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { execFileSync, spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import {
  buildCatalog, codeCatalogGroups, collectKnowledgeMarkdown, createNavigationData, generatedArtifacts, generateKnowledge,
  impactCatalog, parseFrontmatter, renderKnowledgeIndex, searchCatalog, validateRepositoryPath, validateRetrievalCases,
} from './knowledge.mjs'
import { buildPageRecord, buildSearchEntries, normalizeMarkdownLine, readHeadingAnchors } from './generate-docs-index.mjs'
import { affectsWebsite } from './knowledge-ci.mjs'

function markdown(overrides = {}, body = '# Test topic\n\nRead `poi_batch.cpp` and `cv::Mat`.\n') {
  const metadata = { knowledge_id: 'ui.fixture', knowledge_type: 'index', status: 'current', summary: 'Fixture cv::Mat and poi_batch.cpp', aliases: ['IViewResult', '新增结果叠加'], code_paths: ['src/example.cs'], test_paths: [], related: [], ...overrides }
  return `---\n${Object.entries(metadata).map(([key, value]) => `${key}: ${JSON.stringify(value)}`).join('\n')}\n---\n\n${body}`
}

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'colorvision-knowledge-test-'))
  t.after(async () => {
    // Only remove the concrete mkdtemp directory owned by this test.
    const actual = await fs.realpath(root)
    const temporaryRoot = await fs.realpath(os.tmpdir())
    assert.equal(path.dirname(actual), temporaryRoot)
    assert.ok(path.basename(actual).startsWith('colorvision-knowledge-test-'))
    await fs.rm(actual, { recursive: true, force: true })
  })
  await fs.mkdir(path.join(root, 'docs'), { recursive: true })
  await fs.mkdir(path.join(root, 'src'), { recursive: true })
  await fs.writeFile(path.join(root, 'src/example.cs'), 'class Example {}\n')
  await fs.writeFile(path.join(root, 'docs/example.md'), markdown())
  return root
}

test('reads only frontmatter, preserves symbols, and accepts plain scalars', () => {
  const parsed = parseFrontmatter(markdown().replace('knowledge_id: "ui.fixture"', 'knowledge_id: ui.fixture'))
  assert.equal(parsed.metadata.knowledge_id, 'ui.fixture')
  assert.equal(parsed.metadata.summary, 'Fixture cv::Mat and poi_batch.cpp')
  assert.match(parsed.body, /poi_batch\.cpp/u)
  assert.deepEqual(parseFrontmatter('# Text\nknowledge_id: ui.not-metadata').metadata, {})
})

test('retains unrelated nested VitePress frontmatter without parsing it as knowledge', () => {
  const source = markdown().replace('---\n\n#', 'hero:\n  name: "Home"\n  actions:\n    - text: "Open"\n      link: /\nfeatures:\n  - title: "Feature"\n---\n\n#')
  assert.equal(parseFrontmatter(source).metadata.knowledge_id, 'ui.fixture')
})

test('rejects malformed or ambiguous metadata instead of silently reading Markdown', () => {
  assert.throws(() => parseFrontmatter('---\nknowledge_id: ui.fixture\n# body'), /unterminated/u)
  assert.throws(() => parseFrontmatter(markdown().replace('aliases: ["IViewResult","新增结果叠加"]', 'aliases:\n  - IViewResult')), /JSON array/u)
  assert.throws(() => parseFrontmatter(markdown().replace('summary: "Fixture cv::Mat and poi_batch.cpp"', 'summary: |\n  Example')), /plain scalar/u)
  assert.throws(() => parseFrontmatter(markdown().replace('status: "current"', 'status: current\nstatus: planned')), /duplicate field/u)
  assert.throws(() => parseFrontmatter(markdown({ aliases: ['same', 'same'] })), /duplicate value/u)
})

test('mapped files and directories exist, remain inside the repository, and are concrete', async (t) => {
  const root = await fixture(t)
  await validateRepositoryPath(root, 'src/example.cs')
  await validateRepositoryPath(root, 'src')
  await validateRepositoryPath(root, 'src/')
  for (const invalid of ['../outside', '.', 'src/../example.cs', '/tmp/file', 'C:/file', 'https://example.com', 'src/*.cs', 'src\\example.cs', 'src/missing.cs']) {
    await assert.rejects(validateRepositoryPath(root, invalid))
  }
  await assert.rejects(validateRepositoryPath(root, 'src/Example.cs'))
})

test('rejects symlink mappings escaping the repository', async (t) => {
  const root = await fixture(t)
  try {
    await fs.symlink(os.tmpdir(), path.join(root, 'src/escape'), 'junction')
  } catch (error) {
    if (['EPERM', 'EACCES', 'ENOTSUP'].includes(error.code)) { t.skip('Host cannot create test links'); return }
    throw error
  }
  await assert.rejects(validateRepositoryPath(root, 'src/escape'), /escapes repository/u)
  await fs.unlink(path.join(root, 'src/escape'))
})

test('excludes AGENTS, hidden files, generated map and compatibility pages', async (t) => {
  const root = await fixture(t)
  await fs.mkdir(path.join(root, 'docs/knowledge'))
  await fs.writeFile(path.join(root, 'docs/AGENTS.md'), '# Rules without knowledge metadata\n')
  await fs.writeFile(path.join(root, 'docs/_draft.md'), '# Draft without metadata\n')
  await fs.writeFile(path.join(root, 'docs/knowledge/index.md'), '# Generated map\n')
  await fs.mkdir(path.join(root, 'docs/knowledge/code'))
  await fs.writeFile(path.join(root, 'docs/knowledge/code/source-src.md'), '# Generated source map\n')
  await fs.writeFile(path.join(root, 'docs/redirect.md'), '---\nredirect_from_deleted_page: true\nsearch: false\n---\n# Compatibility\n')
  assert.equal((await collectKnowledgeMarkdown(root)).length, 2)
  assert.equal((await buildCatalog(root)).entries.length, 1)
})

test('source maps derive real module directories, preserve cross-module links, and keep root scopes local', async (t) => {
  const root = await fixture(t)
  for (const directory of ['UI/ColorVision.UI', 'UI/ColorVision.ImageEditor', 'Engine/FlowEngineLib', 'Test', '.github/workflows']) {
    await fs.mkdir(path.join(root, directory), { recursive: true })
  }
  for (const source of ['UI/ColorVision.UI/PropertyEditor.cs', 'UI/Directory.Build.props', 'Directory.Build.props', 'Test/ProjectionTests.cs', '.github/workflows/test.yml', 'docs/AGENTS.md']) {
    await fs.writeFile(path.join(root, source), 'Fixture\n')
  }
  await fs.writeFile(path.join(root, 'docs/example.md'), markdown({ code_paths: ['UI'] }))
  const topics = [
    ['ui.exact', ['UI/ColorVision.UI/PropertyEditor.cs', 'UI/ColorVision.UI/']],
    ['ui.image', ['UI/ColorVision.ImageEditor']],
    ['flow.shared', ['UI/ColorVision.UI', 'Engine/FlowEngineLib']],
    ['platform.root-files', ['UI/Directory.Build.props']],
    ['governance.fixture', ['docs/AGENTS.md', '.github/workflows/test.yml', 'Directory.Build.props']],
    ['delivery.unmapped', []],
  ]
  for (const [id, codePaths] of topics) {
    await fs.writeFile(path.join(root, `docs/${id}.md`), markdown({ knowledge_id: id, knowledge_type: 'topic', code_paths: codePaths, test_paths: ['Test/ProjectionTests.cs'], related: ['ui.fixture'] }))
  }
  const catalog = await buildCatalog(root)
  assert.equal(catalog.schema_version, 2)
  assert.deepEqual(catalog.entries.find((entry) => entry.knowledge_id === 'ui.exact').code_scopes, ['UI/ColorVision.UI'])
  assert.deepEqual(catalog.entries.find((entry) => entry.knowledge_id === 'platform.root-files').code_scopes, ['UI'])
  const groups = codeCatalogGroups(catalog)
  assert.deepEqual(groups.map((group) => group.root), ['UI', 'Engine', '.'])
  const ui = groups.find((group) => group.root === 'UI')
  const moduleIds = (scope) => ui.modules.find((module) => module.scope === scope).entries.map((entry) => entry.knowledge_id)
  assert.deepEqual(moduleIds('UI'), ['ui.fixture', 'platform.root-files'])
  assert.deepEqual(moduleIds('UI/ColorVision.UI'), ['flow.shared', 'ui.exact'])
  assert.deepEqual(moduleIds('UI/ColorVision.ImageEditor'), ['ui.image'])
  assert.equal(groups.find((group) => group.root === 'Engine').entries[0].knowledge_id, 'flow.shared')
  assert.ok(groups.find((group) => group.root === '.').modules.some((module) => module.scope === null && module.entries[0].knowledge_id === 'delivery.unmapped'))
  assert.deepEqual(groups.find((group) => group.root === '.').modules.filter((module) => module.scope !== null).map((module) => module.scope), ['.', '.github/workflows', 'docs'])
  assert.equal(groups.some((group) => group.root === 'Test'), false, 'test_paths must not become code ownership')

  const artifacts = generatedArtifacts(catalog)
  const sourceMap = artifacts.get('docs/knowledge/code/source-UI.md')
  assert.match(sourceMap, /关联，不是完整调用图/u)
  assert.match(sourceMap, /\(\.\.\/\.\.\/ui\.exact\.md\)/u)
  assert.equal([...sourceMap.matchAll(/`ui\.exact`/gu)].length, 1, 'multiple code paths in one module must not duplicate its topic')
  assert.doesNotMatch(renderKnowledgeIndex(catalog), /ui\.exact|PropertyEditor\.cs/u, 'main index stays compact')
  const navigation = createNavigationData(catalog)
  assert.equal(navigation.navItems[2].text.root, '源码模块')
  for (const group of groups) {
    const sidebar = navigation.sidebarItems.find((item) => item.link === `/knowledge/code/${group.key}`)
    assert.equal(sidebar.items.length, group.modules.length)
    for (const [index, module] of group.modules.entries()) {
      assert.equal(sidebar.items[index].link, `/knowledge/code/${group.key}#${module.anchor}`)
      assert.deepEqual(sidebar.items[index].items.map((item) => item.link), module.entries.map((entry) => entry.url))
      assert.ok(artifacts.get(`docs/knowledge/code/${group.key}.md`).includes(`{#${module.anchor}}`))
    }
  }
  assert.ok(navigation.navItems.find((item) => item.text.root === '能力领域'))
  assert.equal(impactCatalog(catalog, 'Test/ProjectionTests.cs').length, topics.length, 'test references remain in impact')
  assert.throws(() => codeCatalogGroups({ entries: [{ knowledge_id: 'ui.legacy' }] }), /regenerate/u)
})

test('new source roots and module names are derived without editing the grouping definitions', async (t) => {
  const root = await fixture(t)
  await fs.mkdir(path.join(root, 'NewSubsystem/Module.With.Dots'), { recursive: true })
  await fs.mkdir(path.join(root, 'New Subsystem/Module.With.Dots'), { recursive: true })
  await fs.writeFile(path.join(root, 'docs/example.md'), markdown({ code_paths: ['NewSubsystem/Module.With.Dots', 'New Subsystem/Module.With.Dots'] }))
  const groups = codeCatalogGroups(await buildCatalog(root))
  assert.equal(groups.find((group) => group.root === 'NewSubsystem').modules[0].scope, 'NewSubsystem/Module.With.Dots')
  assert.equal(groups.find((group) => group.root === 'New Subsystem').modules[0].scope, 'New Subsystem/Module.With.Dots')
  assert.equal(new Set(groups.map((group) => group.key)).size, 2)
  assert.ok(groups.every((group) => /^[A-Za-z0-9_-]+$/u.test(group.key)), 'map names stay safe as filenames and Markdown URLs')
})

test('rejects missing fields, duplicate IDs, unknown relations and isolated topics', async (t) => {
  const root = await fixture(t)
  const file = path.join(root, 'docs/example.md')
  await fs.writeFile(file, markdown().replace('status: "current"\n', ''))
  await assert.rejects(buildCatalog(root), /missing status/u)
  await fs.writeFile(file, markdown({ knowledge_type: 'topic' }))
  await assert.rejects(buildCatalog(root), /isolated/u)
  await fs.writeFile(file, markdown({ related: ['ui.missing'] }))
  await assert.rejects(buildCatalog(root), /unknown related/u)
  await fs.writeFile(file, markdown())
  await fs.writeFile(path.join(root, 'docs/duplicate.md'), markdown())
  await assert.rejects(buildCatalog(root), /duplicate knowledge_id/u)
})

test('current search preserves code symbols; planned/historical are opt-in and labeled in navigation', async (t) => {
  const root = await fixture(t)
  await fs.writeFile(path.join(root, 'docs/planned.md'), markdown({ knowledge_id: 'ui.planned', status: 'planned' }))
  await fs.writeFile(path.join(root, 'docs/history.md'), markdown({ knowledge_id: 'ui.history', status: 'historical' }))
  const catalog = await buildCatalog(root)
  for (const query of ['IViewResult', 'cv::Mat', 'poi_batch.cpp', '新增结果叠加']) assert.equal(searchCatalog(catalog, query).length, 1)
  assert.equal(searchCatalog(catalog, 'IViewResult', { all: true }).length, 3)
  assert.match(JSON.stringify(createNavigationData(catalog)), /\[规划\]/u)
  assert.match(JSON.stringify(createNavigationData(catalog)), /\[历史\]/u)
})

test('search finds code symbols touching Chinese text without spaces', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  for (const query of ['IViewResult如何扩展', '可以调用cv::Mat吗', 'poi_batch.cpp实现在哪里']) {
    assert.equal(searchCatalog(catalog, query)[0]?.knowledge_id, 'ui.fixture', query)
  }
  catalog.entries[0].aliases = ['RunAll', 'Code=0', '.NET SDK']
  for (const query of ['RunAll返回Code=0是不是完成了', '只装.NET SDK能编译吗']) {
    assert.equal(searchCatalog(catalog, query)[0]?.knowledge_id, 'ui.fixture', query)
  }
})

test('qualified symbols fall back to bounded owners without splitting underscores or generic members', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  const base = { ...catalog.entries[0], title: 'Fixture', summary: '', code_paths: [], test_paths: [] }
  catalog.entries = [
    { ...base, knowledge_id: 'ui.target', aliases: ['StateStore', 'cv::Mat', 'poi_batch'] },
    { ...base, knowledge_id: 'ui.nearby', aliases: ['StateStoreBackup', 'Namespace', 'Save', 'at', 'batch'] },
  ]
  for (const query of ['StateStore.Save返回了吗', 'Namespace.StateStore.Save', 'cv::Mat::at', 'poi_batch.cpp']) {
    assert.deepEqual(searchCatalog(catalog, query).map((entry) => entry.knowledge_id), ['ui.target'], query)
  }
  assert.deepEqual(searchCatalog(catalog, 'UnknownStore.Save'), [])
  assert.deepEqual(searchCatalog(catalog, 'Namespace.UnknownStore.Save'), [])
  assert.equal(searchCatalog(catalog, 'Namespace.StateStore.Save')[0].score,
    searchCatalog(catalog, 'Namespace.StateStore.Save Namespace.StateStore.Save')[0].score)
})

test('full qualified evidence outranks owner context and exact whole-query aliases remain first', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  const base = { ...catalog.entries[0], title: 'Fixture', code_paths: [], test_paths: [] }
  const query = 'StateStore.Save保存配置失败后如何恢复原始内容并重新加载'
  catalog.entries = [
    { ...base, knowledge_id: 'ui.owner', aliases: ['StateStore'], summary: '保存配置失败后如何恢复原始内容并重新加载' },
    { ...base, knowledge_id: 'ui.full', aliases: ['StateStore.Save'], summary: '' },
    { ...base, knowledge_id: 'ui.nearby', aliases: ['StateStore.SaveBackup'], summary: '保存配置失败后如何恢复原始内容并重新加载' },
  ]
  const ranked = searchCatalog(catalog, query)
  assert.equal(ranked[0].knowledge_id, 'ui.full')
  assert.ok(ranked[0].score < ranked[1].score, 'lexical score must not override qualified evidence')
  catalog.entries.push({ ...base, knowledge_id: 'ui.phrase', aliases: [query], summary: '' })
  assert.equal(searchCatalog(catalog, query)[0].knowledge_id, 'ui.phrase')
})

test('qualified symbol identity outranks incidental module paths and longer namespaces', () => {
  const base = { knowledge_id: 'ui.fixture', title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const query = '引用ColorVision.UI会自动加载插件吗'
  const context = '引用会自动加载插件吗'
  const consumers = [
    { ...base, knowledge_id: 'ui.tests', summary: context, test_paths: ['Test/ColorVision.UI.Tests/PluginTests.cs'] },
    { ...base, knowledge_id: 'ui.desktop', summary: context, code_paths: ['UI/ColorVision.UI.Desktop/Startup.cs'] },
    { ...base, knowledge_id: 'ui.namespace', summary: context, code_paths: ['src/Namespace.ColorVision.UI.More/Feature.cs'] },
    { ...base, knowledge_id: 'ui.module-path', summary: context, code_paths: ['UI/ColorVision.UI/Runtime.cs'] },
  ]
  for (const identity of [{ aliases: ['ColorVision.UI'] }, { title: 'ColorVision.UI' }, { knowledge_id: 'colorvision.ui' }]) {
    const owner = { ...base, knowledge_id: 'ui.owner', ...identity }
    const catalog = { entries: [...consumers, owner] }
    const ranked = searchCatalog(catalog, query)
    assert.equal(ranked[0].knowledge_id, owner.knowledge_id, JSON.stringify(identity))
    assert.ok(ranked[0].score < ranked[1].score, 'named symbol evidence must outrank lexical context')
    assert.equal(ranked.length, consumers.length + 1, 'source-only matches must remain discoverable')
    catalog.entries.push({ ...base, knowledge_id: 'ui.phrase', aliases: [query] })
    assert.equal(searchCatalog(catalog, query)[0].knowledge_id, 'ui.phrase')
  }
})

test('qualified symbol identity is deduplicated and preserves full-member precedence', () => {
  const base = { knowledge_id: 'ui.fixture', title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.a', aliases: ['Module.One'] },
    { ...base, knowledge_id: 'ui.z', aliases: ['Module.Two'] },
  ] }
  const once = searchCatalog(catalog, 'Module.One Module.Two含义')
  assert.deepEqual(once.map((entry) => entry.knowledge_id), ['ui.a', 'ui.z'])
  assert.deepEqual(searchCatalog(catalog, 'Module.One Module.Two Module.Two含义'), once)
  catalog.entries.push({ ...base, knowledge_id: 'ui.both', summary: 'Module.One Module.Two' })
  assert.equal(searchCatalog(catalog, 'Module.One Module.Two含义')[0].knowledge_id, 'ui.both',
    'an exact named symbol must not outrank more complete qualified evidence')

  const memberCatalog = { entries: [
    { ...base, knowledge_id: 'ui.owner', aliases: ['Namespace.StateStore'], summary: '保存配置失败后如何恢复原始内容并重新加载' },
    { ...base, knowledge_id: 'ui.member', summary: 'Namespace.StateStore.Save' },
  ] }
  assert.equal(searchCatalog(memberCatalog, 'Namespace.StateStore.Save保存配置失败后如何恢复原始内容并重新加载')[0].knowledge_id, 'ui.member')
  const unknown = searchCatalog(memberCatalog, 'Namespace.StateStore.Unknown')
  assert.ok(unknown.length > 0)
  assert.ok(unknown.every((entry) => entry.match_kind === 'owner-fallback'), 'owner identity does not establish a requested member')
})

test('qualified symbol identity retains status filters, limits and deterministic ties', () => {
  const base = { knowledge_id: 'ui.fixture', title: 'Fixture', status: 'current', aliases: ['Module.Core'], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.z' },
    { ...base, knowledge_id: 'ui.a', aliases: ['module.core'] },
    { ...base, knowledge_id: 'ui.future', status: 'planned' },
    { ...base, knowledge_id: 'ui.history', status: 'historical' },
    { ...base, knowledge_id: 'ui.hidden', searchable: false },
    { ...base, knowledge_id: 'ui.noise', aliases: [], summary: '引用会自动加载插件吗', code_paths: ['src/Module.Core.Runtime/Load.cs'] },
  ] }
  const query = '引用Module.Core会自动加载插件吗'
  assert.deepEqual(searchCatalog(catalog, query).map((entry) => entry.knowledge_id), ['ui.a', 'ui.z', 'ui.noise'])
  assert.deepEqual(searchCatalog(catalog, query, { all: true }).map((entry) => entry.knowledge_id), ['ui.a', 'ui.future', 'ui.history', 'ui.z', 'ui.noise'])
  assert.deepEqual(searchCatalog(catalog, query, { limit: 1 }).map((entry) => entry.knowledge_id), ['ui.a'])
})

test('repository paths normalize Windows separators and preserve concrete trailing paths', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  const base = { ...catalog.entries[0], title: 'Fixture', summary: '', code_paths: [], test_paths: [] }
  catalog.entries = [
    { ...base, knowledge_id: 'ui.exact', aliases: [], code_paths: ['Native/include/native_buffer.h'] },
    { ...base, knowledge_id: 'ui.file', aliases: ['native_buffer.h'] },
    { ...base, knowledge_id: 'ui.generic', aliases: ['Native', 'include', 'h', 'buffer'] },
  ]
  const forward = searchCatalog(catalog, 'Native/include/native_buffer.h实现在哪里')
  const windows = searchCatalog(catalog, 'Native\\include\\native_buffer.h实现在哪里')
  assert.deepEqual(windows, forward)
  assert.equal(forward[0].knowledge_id, 'ui.exact')
  assert.ok(!forward.some((entry) => entry.knowledge_id === 'ui.generic'))
  assert.deepEqual(searchCatalog(catalog, 'C:\\checkout\\Native\\include\\native_buffer.h').map((entry) => entry.knowledge_id), ['ui.exact', 'ui.file'])
})

test('qualified owners and longer path tails outrank bare names with more unrelated context', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  const base = { ...catalog.entries[0], title: 'Fixture', summary: '', code_paths: [], test_paths: [] }
  const context = '保存配置失败后如何恢复原始内容并重新加载'
  catalog.entries = [
    { ...base, knowledge_id: 'ui.short', aliases: ['StateStore', 'native_buffer.h'], summary: context },
    { ...base, knowledge_id: 'ui.owner', aliases: ['Namespace.StateStore'] },
    { ...base, knowledge_id: 'ui.path', aliases: [], code_paths: ['Native/include/native_buffer.h'] },
  ]
  assert.equal(searchCatalog(catalog, `Namespace.StateStore.Save${context}`)[0].knowledge_id, 'ui.owner')
  assert.equal(searchCatalog(catalog, `C:/checkout/Native/include/native_buffer.h${context}`)[0].knowledge_id, 'ui.path')
})

test('owner fallback retains status filtering and deterministic ties', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  const base = { ...catalog.entries[0], title: 'Fixture', summary: '', aliases: ['StateStore'], code_paths: [], test_paths: [] }
  catalog.entries = [
    { ...base, knowledge_id: 'ui.z' },
    { ...base, knowledge_id: 'ui.a' },
    { ...base, knowledge_id: 'ui.future', status: 'planned' },
    { ...base, knowledge_id: 'ui.hidden', searchable: false },
  ]
  assert.deepEqual(searchCatalog(catalog, 'StateStore.Save').map((entry) => entry.knowledge_id), ['ui.a', 'ui.z'])
  assert.deepEqual(searchCatalog(catalog, 'StateStore.Save', { all: true }).map((entry) => entry.knowledge_id), ['ui.a', 'ui.future', 'ui.z'])
})

test('explicitly described owners outrank equally specific incidental source references', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  const base = { ...catalog.entries[0], title: 'Fixture', summary: '', code_paths: [], test_paths: [] }
  const context = '保存配置失败后如何恢复原始内容并重新加载'
  catalog.entries = [
    { ...base, knowledge_id: 'ui.consumer', aliases: [], code_paths: ['src/StateStore.cs'], summary: context },
    { ...base, knowledge_id: 'ui.owner', aliases: ['StateStore'] },
  ]
  assert.equal(searchCatalog(catalog, `StateStore.Save${context}`)[0].knowledge_id, 'ui.owner')
  // A source-only owner must remain discoverable when it has no descriptive alias.
  catalog.entries.pop()
  assert.equal(searchCatalog(catalog, 'StateStore.Save')[0].knowledge_id, 'ui.consumer')
})

test('bare code symbols outrank generic question text regardless of query casing', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const context = '保存配置失败后如何恢复原始内容并重新加载旧字典ID'
  for (const symbol of ['StateStore', 'restoreDatabaseAsync', 'HImage', 'build_dependency_sql', 'MYSQL_PWD']) {
    const catalog = { entries: [
      { ...base, knowledge_id: 'ui.target', aliases: [symbol] },
      ...Array.from({ length: 6 }, (_, index) => ({ ...base, knowledge_id: `ui.noise-${index}`, summary: context })),
    ] }
    for (const spelling of [symbol, symbol.toLowerCase(), symbol.toUpperCase()]) {
      const ranked = searchCatalog(catalog, `${spelling}${context}`)
      assert.equal(ranked[0].knowledge_id, 'ui.target', spelling)
      assert.equal(ranked[0].match_kind, 'code-symbol')
      assert.ok(ranked[0].score < ranked[1].score, 'code evidence must beat more generic lexical matches')
    }
  }
})

test('bare symbol boundaries and explicit names outrank incidental source-only evidence', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const context = '保存配置失败后如何恢复原始内容并重新加载'
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.longer', aliases: ['StateStoreBackup', 'OtherStateStore', 'StateStore_legacy'], summary: context },
    { ...base, knowledge_id: 'ui.path', code_paths: ['src/StateStore.cs'], summary: context },
    { ...base, knowledge_id: 'ui.target', aliases: ['StateStore'] },
  ] }
  assert.deepEqual(searchCatalog(catalog, `statestore${context}`).map((entry) => entry.knowledge_id), ['ui.target', 'ui.path', 'ui.longer'])
  catalog.entries.pop()
  const sourceOnly = searchCatalog(catalog, `statestore${context}`)
  assert.equal(sourceOnly[0].knowledge_id, 'ui.path')
  assert.equal(sourceOnly[0].match_kind, 'code-symbol')
  assert.equal(sourceOnly[1].match_kind, 'text', 'a longer symbol is not complete code identity')
})

test('ordinary English words and short acronyms do not gain bare-symbol priority', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const context = '保存配置失败后如何恢复原始内容并重新加载'
  for (const word of ['Save', 'backup', 'ID', 'SQL', 'XML', 'README']) {
    const catalog = { entries: [
      { ...base, knowledge_id: 'ui.word', aliases: [word] },
      { ...base, knowledge_id: 'ui.context', summary: context },
    ] }
    const ranked = searchCatalog(catalog, `${word}${context}`)
    assert.equal(ranked[0].knowledge_id, 'ui.context', word)
    assert.ok(ranked.every((entry) => entry.match_kind === 'text'))
  }
})

test('broad code-shaped technology names do not displace more specific known query words', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.target', aliases: ['cvsln'], summary: '打开源目录生成缓存' },
    ...Array.from({ length: 6 }, (_, index) => ({ ...base, knowledge_id: `ui.sqlite-${index}`, aliases: ['SQLite'] })),
  ] }
  const ranked = searchCatalog(catalog, '打开cvsln会在源目录生成SQLite缓存吗')
  assert.equal(ranked[0].knowledge_id, 'ui.target')
  assert.ok(ranked.every((entry) => entry.match_kind === 'text'), 'the broad token must remain lexical in this question')
  assert.equal(searchCatalog(catalog, 'SQLite未登记的UnknownToken和123')[0].match_kind, 'code-symbol',
    'unmatched words and numbers must not suppress known symbols')
  const typed = { entries: [{ ...base, knowledge_id: 'ui.reader', aliases: ['XMLReader'] }] }
  assert.equal(searchCatalog(typed, 'xmlreader如何使用')[0].match_kind, 'code-symbol', 'do not blacklist acronym-prefixed types')
})

test('rarer ordinary words only demote a code symbol when they locate a competing topic', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const context = '保存配置失败后如何恢复原始内容并重新加载'
  for (const word of ['backup', 'Save', 'ID']) {
    const catalog = { entries: [
      { ...base, knowledge_id: 'ui.target', aliases: ['StateStore', word] },
      { ...base, knowledge_id: 'ui.consumer', code_paths: ['src/StateStore.cs'] },
      ...Array.from({ length: 6 }, (_, index) => ({ ...base, knowledge_id: `ui.noise-${index}`, summary: context })),
    ] }
    const ranked = searchCatalog(catalog, `StateStore ${word}${context}`)
    assert.equal(ranked[0].knowledge_id, 'ui.target', word)
    assert.equal(ranked[0].match_kind, 'code-symbol')
  }
})

test('mixed-query disambiguation demotes broad symbols individually, not every code symbol', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.type', aliases: ['StateStore'] },
    { ...base, knowledge_id: 'ui.format', aliases: ['cvsln'], summary: '源目录生成缓存' },
    ...Array.from({ length: 6 }, (_, index) => ({ ...base, knowledge_id: `ui.sqlite-${index}`, aliases: ['SQLite'] })),
  ] }
  const query = 'StateStore打开cvsln会在源目录生成SQLite缓存吗'
  const ranked = searchCatalog(catalog, query)
  assert.equal(ranked[0].knowledge_id, 'ui.type')
  assert.equal(ranked[0].match_kind, 'code-symbol')
  assert.ok(ranked.filter((entry) => entry.knowledge_id !== 'ui.type').every((entry) => entry.match_kind === 'text'))
})

test('bare symbols preserve whole-query and qualified-member precedence without splitting owners', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const query = 'StateStore.Save与BackupStore的保存范围'
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.bare', aliases: ['StateStore', 'BackupStore', 'Save'], summary: '保存范围' },
    { ...base, knowledge_id: 'ui.owner', aliases: ['StateStore'] },
    { ...base, knowledge_id: 'ui.member', aliases: ['StateStore.Save'] },
  ] }
  assert.equal(searchCatalog(catalog, query)[0].knowledge_id, 'ui.member')
  catalog.entries.push({ ...base, knowledge_id: 'ui.phrase', aliases: [query] })
  assert.equal(searchCatalog(catalog, query)[0].knowledge_id, 'ui.phrase')
  const unknown = searchCatalog(catalog, 'Namespace.StateStore.Unknown')
  assert.ok(unknown.length > 0)
  assert.ok(unknown.every((entry) => entry.match_kind === 'owner-fallback'))
  assert.deepEqual(searchCatalog(catalog, 'UnknownStore.Save'), [], 'do not split a qualified token into a bare member')
})

test('bare symbol evidence counts complete distinct symbols before named identities', () => {
  const base = { title: 'Fixture', status: 'current', aliases: [], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.one', aliases: ['StateStore', 'StateStore说明'] },
    { ...base, knowledge_id: 'ui.both', summary: 'StateStore BackupStore' },
  ] }
  const once = searchCatalog(catalog, 'StateStore BackupStore含义')
  assert.equal(once[0].knowledge_id, 'ui.both')
  assert.deepEqual(searchCatalog(catalog, 'StateStore BackupStore BACKUPSTORE含义'), once)
})

test('bare symbol ranking retains status filters, result limits and deterministic ties', () => {
  const base = { title: 'Fixture', status: 'current', aliases: ['StateStore'], summary: '', source: 'docs/fixture.md', code_paths: [], test_paths: [] }
  const catalog = { entries: [
    { ...base, knowledge_id: 'ui.z' },
    { ...base, knowledge_id: 'ui.a', aliases: ['statestore'] },
    { ...base, knowledge_id: 'ui.future', status: 'planned' },
    { ...base, knowledge_id: 'ui.history', status: 'historical' },
    { ...base, knowledge_id: 'ui.hidden', searchable: false },
    { ...base, knowledge_id: 'ui.noise', aliases: [], summary: '保存配置失败后如何恢复原始内容并重新加载' },
  ] }
  const query = 'STATESTORE保存配置失败后如何恢复原始内容并重新加载'
  assert.deepEqual(searchCatalog(catalog, query).map((entry) => entry.knowledge_id), ['ui.a', 'ui.z', 'ui.noise'])
  assert.deepEqual(searchCatalog(catalog, query, { all: true }).map((entry) => entry.knowledge_id), ['ui.a', 'ui.future', 'ui.history', 'ui.z', 'ui.noise'])
  assert.deepEqual(searchCatalog(catalog, query, { limit: 1 }).map((entry) => entry.knowledge_id), ['ui.a'])
})

test('generation is deterministic and check detects changed, missing and extra generated data', async (t) => {
  const root = await fixture(t)
  await assert.rejects(generateKnowledge(root, true), /stale or missing/u)
  await generateKnowledge(root)
  await generateKnowledge(root, true)
  const index = await fs.readFile(path.join(root, 'docs/knowledge/index.md'), 'utf8')
  assert.match(index, /\(\.\/code\/source-src\.md\)/u)
  assert.match(index, /\(\.\/domains\/ui\.md\)/u)
  assert.equal([...index.matchAll(/\(\.\/domains\/[a-z]+\.md\)/gu)].length, 11)
  assert.doesNotMatch(index, /ui\.fixture|Test topic/u)
  const domain = await fs.readFile(path.join(root, 'docs/knowledge/domains/ui.md'), 'utf8')
  assert.match(domain, /\(\.\.\/\.\.\/example\.md\)/u)
  assert.match(domain, /ui\.fixture/u)
  for (const field of ['editLink', 'prev', 'next']) assert.match(domain, new RegExp(`${field}: false`))
  assert.equal((await buildCatalog(root)).entries.length, 1)
  const sourceMapPath = path.join(root, 'docs/knowledge/code/source-src.md')
  await fs.appendFile(sourceMapPath, '\nManual source-map edit.\n')
  await assert.rejects(generateKnowledge(root, true), /code\/source-src\.md/u)
  await generateKnowledge(root)
  await fs.unlink(sourceMapPath)
  await assert.rejects(generateKnowledge(root, true), /code\/source-src\.md/u)
  await generateKnowledge(root)
  await fs.appendFile(path.join(root, 'docs/knowledge/domains/ui.md'), '\nManual edit.\n')
  await assert.rejects(generateKnowledge(root, true), /domains\/ui\.md/u)
  await generateKnowledge(root)
  await fs.appendFile(path.join(root, 'docs/example.md'), '\nChanged implementation guidance.\n')
  await assert.rejects(generateKnowledge(root, true), /stale or missing/u)
  await generateKnowledge(root)
  await fs.appendFile(path.join(root, 'docs/knowledge/catalog.json'), '\n')
  await assert.rejects(generateKnowledge(root, true), /catalog\.json/u)
})

test('checks reject obsolete or unknown generated maps and generation preserves them for review', async (t) => {
  const root = await fixture(t)
  await generateKnowledge(root)
  for (const source of ['code/obsolete.md', 'domains/unknown.md']) {
    const target = path.join(root, 'docs/knowledge', source)
    const content = '# Handwritten or obsolete map; preserve for review.\n'
    await fs.writeFile(target, content)
    await assert.rejects(generateKnowledge(root, true), /Unexpected generated knowledge maps/u)
    await assert.rejects(generateKnowledge(root), /Unexpected generated knowledge maps/u)
    assert.equal(await fs.readFile(target, 'utf8'), content)
    await fs.unlink(target)
  }
  await generateKnowledge(root, true)
})

test('reverse mappings include files, directories, document edits and deleted path queries', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  assert.equal(impactCatalog(catalog, 'src/example.cs').length, 1)
  assert.equal(impactCatalog(catalog, 'src').length, 1)
  assert.equal(impactCatalog(catalog, 'docs/example.md').length, 1)
  assert.equal(impactCatalog(catalog, 'src/example.cs.extra').length, 0)
  catalog.entries[0].code_paths = ['src']
  assert.equal(impactCatalog(catalog, 'src/deleted.cs').length, 1)
  assert.throws(() => impactCatalog(catalog, '../escape'), /traversal/u)
})

test('search CLI labels owner fallback and bare code symbols while impact retains source mappings', async (t) => {
  const root = await fixture(t)
  await fs.writeFile(path.join(root, 'docs/example.md'), markdown({ aliases: ['StateStore'], summary: 'State persistence.' }))
  await generateKnowledge(root)
  const scriptPath = path.join(root, 'docs/.vitepress/scripts/knowledge.mjs')
  await fs.mkdir(path.dirname(scriptPath), { recursive: true })
  await fs.copyFile(fileURLToPath(new URL('./knowledge.mjs', import.meta.url)), scriptPath)
  const output = execFileSync(process.execPath, [scriptPath, 'search', 'StateStore.Save'], { encoding: 'utf8', cwd: root })
  assert.match(output, /\[current\] ui.fixture/u)
  assert.match(output, /docs\/example\.md/u)
  assert.match(output, /match: owner-fallback/u)
  assert.match(output, /does not verify the requested member/u)
  assert.doesNotMatch(output, /src\/example\.cs/u)
  const bare = execFileSync(process.execPath, [scriptPath, 'search', 'statestore如何保存'], { encoding: 'utf8', cwd: root })
  assert.match(bare, /\[current\] ui.fixture/u)
  assert.match(bare, /match: code-symbol/u)
  assert.doesNotMatch(bare, /src\/example\.cs/u)
  const impact = execFileSync(process.execPath, [scriptPath, 'impact', 'src/example.cs'], { encoding: 'utf8', cwd: root })
  assert.match(impact, /mapped: src\/example\.cs/u)
})

async function lookupCliFixture(t) {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  const entry = catalog.entries[0]
  catalog.entries = Array.from({ length: 14 }, (_, index) => ({ ...entry,
    knowledge_id: `ui.fixture-${String(index).padStart(2, '0')}`,
    status: index === 13 ? 'planned' : 'current', aliases: ['CLIStore', '--all', '--limit', '--help'],
  }))
  const catalogPath = path.join(root, 'docs/knowledge/catalog.json')
  await fs.mkdir(path.dirname(catalogPath), { recursive: true })
  await fs.writeFile(catalogPath, JSON.stringify(catalog))
  const scriptPath = path.join(root, 'docs/.vitepress/scripts/knowledge.mjs')
  await fs.mkdir(path.dirname(scriptPath), { recursive: true })
  await fs.copyFile(fileURLToPath(new URL('./knowledge.mjs', import.meta.url)), scriptPath)
  const run = (...args) => spawnSync(process.execPath, [scriptPath, ...args], { encoding: 'utf8', cwd: os.tmpdir() })
  const ids = (output) => [...output.matchAll(/^\[(?:current|planned)\] (\S+) —/gmu)].map((match) => match[1])
  return { root, catalog, catalogPath, run, ids }
}

test('search CLI reports truncation and accepts an explicit result window without changing rank', async (t) => {
  const { catalog, run, ids } = await lookupCliFixture(t)
  const full = searchCatalog(catalog, 'CLIStore', { limit: catalog.entries.length }).map((entry) => entry.knowledge_id)
  const normal = run('search', 'CLIStore')
  assert.equal(normal.status, 0, normal.stderr)
  assert.deepEqual(ids(normal.stdout), full.slice(0, 12))
  assert.match(normal.stdout, /12 of 13 match\(es\) shown \(limit 12\)/u)
  for (const args of [['CLIStore', '--limit', '3'], ['--limit=3', 'CLIStore'], ['--limit', '3', 'CLIStore']]) {
    const result = run('search', ...args)
    assert.equal(result.status, 0, result.stderr)
    assert.deepEqual(ids(result.stdout), full.slice(0, 3))
    assert.match(result.stdout, /3 of 13 match\(es\) shown \(limit 3\)/u)
  }
  const wide = run('search', 'CLIStore', '--limit', '100')
  assert.equal(wide.status, 0, wide.stderr)
  assert.deepEqual(ids(wide.stdout), full)
  assert.match(wide.stdout, /13 match\(es\)\./u)
  const empty = run('search', 'DefinitelyAbsentSymbol_297abc', '--limit', '1')
  assert.equal(empty.status, 0, empty.stderr)
  assert.deepEqual(ids(empty.stdout), [])
  assert.match(empty.stdout, /0 match\(es\)\./u)
})

test('search CLI combines all-status lookup with limits and preserves literal option text', async (t) => {
  const { catalog, run, ids } = await lookupCliFixture(t)
  const result = run('search', '--all', 'CLIStore', '--limit=14')
  assert.equal(result.status, 0, result.stderr)
  assert.deepEqual(ids(result.stdout), searchCatalog(catalog, 'CLIStore', { all: true, limit: 14 }).map((entry) => entry.knowledge_id))
  assert.match(result.stdout, /\[planned\] ui.fixture-13/u)
  for (const literal of ['--all', '--limit', '--help']) {
    const result = run('search', '--limit', '2', '--', literal)
    assert.equal(result.status, 0, result.stderr)
    assert.deepEqual(ids(result.stdout), searchCatalog(catalog, literal, { limit: 2 }).map((entry) => entry.knowledge_id))
    assert.doesNotMatch(result.stdout, /\[planned\]/u)
  }
})

test('search CLI rejects invalid limits and unknown options instead of changing the query', async (t) => {
  const { run } = await lookupCliFixture(t)
  for (const value of ['0', '-1', '1.5', '1e2', 'Infinity', '9007199254740992', '', 'abc']) {
    const result = run('search', 'CLIStore', `--limit=${value}`)
    assert.equal(result.status, 1, value)
    assert.match(result.stderr, /--limit requires a positive safe integer/u)
    assert.equal(result.stdout, '')
  }
  for (const args of [['CLIStore', '--limit'], ['CLIStore', '--limit', '--all']]) {
    const result = run('search', ...args)
    assert.equal(result.status, 1)
    assert.match(result.stderr, /--limit requires a positive safe integer/u)
  }
  const duplicate = run('search', 'CLIStore', '--limit=2', '--limit', '3')
  assert.equal(duplicate.status, 1)
  assert.match(duplicate.stderr, /--limit may only be supplied once/u)
  const unknown = run('search', 'CLIStore', '--limt', '2')
  assert.equal(unknown.status, 1)
  assert.match(unknown.stderr, /Unknown search option.*--limt/u)
  const empty = run('search', '--all', '--limit', '2')
  assert.equal(empty.status, 1)
  assert.match(empty.stderr, /search requires a query/u)
})

test('impact CLI remains exhaustive and rejects search options and multiple paths', async (t) => {
  const { catalog, catalogPath, run, ids } = await lookupCliFixture(t)
  const result = run('impact', 'src/example.cs')
  assert.equal(result.status, 0, result.stderr)
  assert.equal(ids(result.stdout).length, 14)
  assert.match(result.stdout, /\[planned\]/u)
  assert.match(result.stdout, /mapped: src\/example\.cs/u)
  for (const args of [['src/example.cs', '--limit', '2'], ['src/example.cs', '--all']]) {
    const result = run('impact', ...args)
    assert.equal(result.status, 1)
    assert.match(result.stderr, /Unknown impact option/u)
  }
  const multiple = run('impact', 'src/example.cs', 'docs/example.md')
  assert.equal(multiple.status, 1)
  assert.match(multiple.stderr, /impact requires exactly one repository-relative path/u)
  assert.equal(run('impact', 'src/deleted.cs').status, 0)
  catalog.entries[0].code_paths = [...catalog.entries[0].code_paths, 'src/with space.cs']
  await fs.writeFile(catalogPath, JSON.stringify(catalog))
  const spaced = run('impact', 'src/with space.cs')
  assert.equal(spaced.status, 0, spaced.stderr)
  assert.deepEqual(ids(spaced.stdout), ['ui.fixture-00'])
})

test('lookup CLI stays dependency-free and read-only while help needs no catalog', async (t) => {
  const { root, catalogPath, run } = await lookupCliFixture(t)
  const before = await fs.readFile(catalogPath, 'utf8')
  await fs.writeFile(path.join(root, 'docs/example.md'), '---\ninvalid metadata')
  await fs.unlink(path.join(root, 'src/example.cs'))
  assert.equal(run('search', 'CLIStore', '--limit', '1').status, 0)
  assert.equal(run('impact', 'src/example.cs').status, 0)
  assert.equal(await fs.readFile(catalogPath, 'utf8'), before)
  await assert.rejects(fs.stat(path.join(root, 'node_modules')), { code: 'ENOENT' })
  await fs.unlink(catalogPath)
  for (const args of [['--help'], ['search', '--help'], ['impact', '--help']]) {
    const result = run(...args)
    assert.equal(result.status, 0, result.stderr)
    assert.match(result.stdout, /--limit/u)
    assert.match(result.stdout, /impact/u)
    assert.equal(result.stderr, '')
  }
  assert.equal(run('search', 'CLIStore').status, 1)
})

test('website search text retains underscores and generic code identifiers', () => {
  assert.equal(normalizeMarkdownLine('Read `poi_batch.cpp` and `List<T>` or **important** `cv::Mat`.'), 'Read poi_batch.cpp and List<T> or important cv::Mat.')
})

test('fixed query acceptance checks expected IDs and the top-five window', async (t) => {
  const root = await fixture(t)
  const catalog = await buildCatalog(root)
  assert.equal(validateRetrievalCases(catalog, { cases: [{ query: 'IViewResult', expected_any: ['ui.fixture'], include_planned: false }] }).length, 1)
  assert.throws(() => validateRetrievalCases(catalog, { cases: [{ query: 'MissingSymbol', expected_any: ['ui.fixture'], include_planned: false }] }), /top 5/u)
  assert.throws(() => validateRetrievalCases(catalog, { cases: [{ query: 'IViewResult', expected_any: ['ui.unknown'], include_planned: false }] }), /Invalid retrieval case/u)
})

test('website records derive identity, state and mappings from the same source catalog', async (t) => {
  const root = await fixture(t)
  const knowledge = (await buildCatalog(root)).entries[0]
  const page = await buildPageRecord(path.join(root, knowledge.source), knowledge, '<h1 id="test-topic">Test topic</h1>')
  const entries = buildSearchEntries(page)
  assert.equal(page.sectionKey, 'ui')
  assert.equal(entries[0].knowledge_id, 'ui.fixture')
  assert.equal(entries[0].relativePath, 'example.md')
  assert.equal(entries[0].sourcePath, 'docs/example.md')
  assert.equal(entries[0].status, 'current')
  assert.deepEqual(entries[0].code_paths, ['src/example.cs'])
})

test('website uses actual HTML anchors for punctuation, custom IDs, Unicode and repeated headings', async (t) => {
  const root = await fixture(t)
  const source = markdown({}, '# Test topic\n\n## 1. Café / 类名\nText.\n\n## 重复 {#custom-anchor}\nFirst.\n\n## 重复\nSecond.\n\n## 重复\nThird.\n')
  await fs.writeFile(path.join(root, 'docs/example.md'), source)
  const knowledge = (await buildCatalog(root)).entries[0]
  const html = '<h1 id="test-topic">Test topic</h1><h2 id="_1-cafe-类名">1. Café / 类名<a class="header-anchor">&ZeroWidthSpace;</a></h2><h2 id="custom-anchor">重复</h2><h2 id="重复">重复</h2><h2 id="重复-1">重复</h2>'
  const page = await buildPageRecord(path.join(root, knowledge.source), knowledge, html)
  const sections = buildSearchEntries(page).filter((entry) => entry.kind === 'section')
  assert.equal(sections.length, 4)
  assert.deepEqual(sections.map((entry) => decodeURIComponent(entry.url.split('#')[1])), ['_1-cafe-类名', 'custom-anchor', '重复', '重复-1'])
  assert.ok(sections.some((entry) => entry.url.endsWith('#custom-anchor')))
  assert.equal(readHeadingAnchors(html)[1].text, '1. Café / 类名')
  await assert.rejects(buildPageRecord(path.join(root, knowledge.source), knowledge, '<h1 id="test-topic">Test topic</h1>'), /Markdown headings/u)
})

test('CI keeps code-only PRs on the lightweight checks while building docs and tooling changes', () => {
  assert.equal(affectsWebsite(['UI/ColorVision.UI/PropertyEditor/PropertyGrid.cs']), false)
  assert.equal(affectsWebsite(['Plugins/README.md', 'Projects/ProjectKB/README.md']), false)
  for (const source of ['docs/example.md', 'docs/.vitepress/scripts/knowledge.mjs', 'AGENTS.md', 'UI/AGENTS.md', 'package-lock.json', '.github/workflows/deploy.yml', 'README.md']) {
    assert.equal(affectsWebsite([source]), true, source)
  }
})

test('CI scope command handles manual runs and fails closed on invalid PR revisions', async (t) => {
  const root = await fixture(t)
  const command = fileURLToPath(new URL('./knowledge-ci.mjs', import.meta.url))
  const output = path.join(root, 'ci-output.txt')
  execFileSync(process.execPath, [command], { cwd: root, env: { ...process.env, KNOWLEDGE_EVENT_NAME: 'workflow_dispatch', GITHUB_OUTPUT: output } })
  assert.equal(await fs.readFile(output, 'utf8'), 'website_changed=true\n')
  const failure = spawnSync(process.execPath, [command], { cwd: root, encoding: 'utf8', env: { ...process.env,
    KNOWLEDGE_EVENT_NAME: 'pull_request', KNOWLEDGE_BASE_SHA: '--invalid-ref', KNOWLEDGE_HEAD_SHA: 'missing', GITHUB_OUTPUT: output,
  } })
  assert.equal(failure.status, 1)
  assert.match(failure.stderr, /invalid PR commit SHA/u)
  assert.equal(await fs.readFile(output, 'utf8'), 'website_changed=true\n')
})
