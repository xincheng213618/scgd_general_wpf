import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { createHash } from 'node:crypto'
import test from 'node:test'
import { checkHelpBundle, createHelpBundle, extractHelpSection, writeHelpBundle } from './algorithm-help.mjs'

const fixture = content => `---\nknowledge_id: "algorithms.grid-distortion"\n---\nOutside the help page.\n<!-- algorithm-help:start -->\n${content}\n<!-- algorithm-help:end -->\nOutside after the help page.`
const distortion = {
  sourcePath: 'docs/04-api-reference/algorithms/detectors/grid-distortion-v2.md',
  markdown: fixture('## 点阵畸变测量原理 {#algorithm-principles}\n\n$D=100\\frac{a-b}{b}$\n\n<GridDistortionDemo />\n\n### 指标说明 {#metrics}\n\n| 指标 | 单位 |\n| --- | --- |\n| TV | % |'),
}
const inputs = {
  sources: [distortion],
  demoScript: 'export function mountGridDistortionDemo() {}\n',
  demoCss: '.cv-grid-demo { display: block; }\n',
  articleCss: 'body { color: black; }\n',
  bootScript: "import { mountGridDistortionDemo } from './grid-distortion-demo.js';\n",
}

test('help extraction requires one nonempty ordered marker pair', () => {
  assert.equal(extractHelpSection(fixture('Content')), 'Content')
  assert.throws(() => extractHelpSection('No markers'), /exactly one/u)
  assert.throws(() => extractHelpSection(fixture('') + '\n<!-- algorithm-help:start -->'), /exactly one/u)
  assert.throws(() => extractHelpSection('<!-- algorithm-help:end -->Content<!-- algorithm-help:start -->'), /empty or.*reversed/u)
})

test('a stable knowledge topic builds local formulas, demo resources, and section navigation', async () => {
  const bundle = await createHelpBundle(inputs)
  const html = bundle.get('algorithms.grid-distortion.html')
  assert.match(html, /<svg/u)
  assert.match(html, /data-grid-distortion-demo/u)
  assert.match(html, /href="#metrics"/u)
  assert.match(html, /connect-src 'none'/u)
  assert.doesNotMatch(html, /GridDistortionDemo|Outside the help page|Outside after/u)
  assert.equal(bundle.get('grid-distortion-demo.js'), inputs.demoScript)
  assert.equal(bundle.get('grid-distortion-demo.css'), inputs.demoCss)
  const manifest = JSON.parse(bundle.get('index.json'))
  assert.equal(manifest[0].id, 'algorithms.grid-distortion')
  assert.equal(manifest[0].relativePath, 'algorithms.grid-distortion.html')
  assert.equal(manifest[0].markdownRelativePath, 'algorithms.grid-distortion.md')
  const markdown = bundle.get(manifest[0].markdownRelativePath)
  assert.equal(markdown, extractHelpSection(distortion.markdown))
  assert.match(markdown, /\$D=100\\frac\{a-b\}\{b\}\$/u)
  assert.match(markdown, /<GridDistortionDemo \/>/u, 'The Markdown retains source component markup without executing it.')
  assert.doesNotMatch(markdown, /\r|Outside the help page|Outside after/u)
  assert.equal(manifest[0].markdownSha256, createHash('sha256').update(markdown, 'utf8').digest('hex'))
  assert.match(manifest[0].onlineUrl, /^https:\/\/xincheng213618\.github\.io\/scgd_general_wpf\//u)
  assert.match(manifest[0].sourceSha256, /^[a-f\d]{64}$/u)
  assert.deepEqual(await createHelpBundle(inputs), bundle, 'Generation must be deterministic within one process.')
  const windowsInputs = Object.fromEntries(Object.entries(inputs).map(([key, value]) => [key, typeof value === 'string' ? value.replaceAll('\n', '\r\n') : value.map(source => ({ ...source, markdown: source.markdown.replaceAll('\n', '\r\n') }))]))
  assert.deepEqual(await createHelpBundle(windowsInputs), bundle, 'Git CRLF conversion must not change generated help or its content hash.')
  const restyled = await createHelpBundle({ ...inputs, articleCss: inputs.articleCss + 'body { color: navy; }\n' })
  const [restyledTopic] = JSON.parse(restyled.get('index.json'))
  assert.notEqual(restyledTopic.sourceSha256, manifest[0].sourceSha256)
  assert.equal(restyledTopic.markdownSha256, manifest[0].markdownSha256, 'Markdown integrity hashes source text independently of HTML styling.')
})

test('unknown components and external-resource content cannot silently enter the offline package', async () => {
  const withContent = content => ({ ...inputs, sources: [{ ...distortion, markdown: fixture('## 原理 {#algorithm-principles}\n\n' + content) }] })
  await assert.rejects(createHelpBundle(withContent('<UnknownDemo />')), /Unsupported.*UnknownDemo/u)
  await assert.rejects(createHelpBundle(withContent('<script src="https://example.com/a.js"></script>')), /executable or external-resource/u)
  await assert.rejects(createHelpBundle(withContent('<link rel="stylesheet" href="https://example.com/a.css">')), /executable or external-resource/u)
  await assert.rejects(createHelpBundle(withContent('![external](https://example.com/image.png)')), /external resource/u)
  await assert.rejects(createHelpBundle(withContent('![missing](local-image.png)')), /undelivered asset/u)
  await assert.rejects(createHelpBundle(withContent('<svg><image href="https://example.com/image.png" /></svg>')), /external resource/u)
  await assert.rejects(createHelpBundle(withContent('[missing](#outside)')), /Missing offline help anchor/u)
})

test('math inequalities and code generics are rendered before component validation', async () => {
  const markdown = fixture('## 计算 {#algorithm-principles}\n\n$$\nT<B<T_{\\mathrm{dim}}\n$$\n\n`ITemplate<T>`\n\n```html\n<script src="example.js"></script>\n```\n\n```vue\n<GridDistortionDemo />\n```')
  const bundle = await createHelpBundle({ ...inputs, sources: [{ ...distortion, markdown }] })
  const html = bundle.get('algorithms.grid-distortion.html')
  assert.match(html, /<mjx-container/u)
  assert.match(html, /ITemplate&lt;T&gt;/u)
  assert.doesNotMatch(html, /<script src="example.js"/u)
  assert.match(html, /GridDistortionDemo/u)
  assert.doesNotMatch(html, /data-grid-distortion-demo|src="boot.js"/u, 'Code examples must not become live widgets.')
})

test('multiple canonical topics share assets and preserve offline and website link boundaries', async () => {
  const other = {
    sourcePath: 'docs/04-api-reference/algorithms/detectors/example.md',
    markdown: fixture('## 另一个算法原理 {#algorithm-principles}\n\n<svg viewBox="0 0 100 40" role="img"><title>计算图</title><path d="M0 0L10 10" /></svg>\n\n[离线指标](./grid-distortion-v2.md#metrics) · [网站其他章节](./grid-distortion-v2.md#outside) · [其他主题](../overview.md)').replace('algorithms.grid-distortion', 'algorithms.example'),
  }
  const bundle = await createHelpBundle({ ...inputs, sources: [distortion, other] })
  const manifest = JSON.parse(bundle.get('index.json'))
  assert.equal(manifest.length, 2)
  assert.equal(manifest[1].title, '另一个算法原理')
  assert.equal([...bundle.keys()].filter(key => key === 'algorithm-help.css').length, 1)
  const html = bundle.get('algorithms.example.html')
  assert.equal(bundle.get('algorithms.example.md'), extractHelpSection(other.markdown))
  assert.match(bundle.get('algorithms.example.md'), /<svg viewBox=/u, 'Saved figures remain source markup in Markdown.')
  assert.match(html, /href="algorithms.grid-distortion.html#metrics"/u)
  assert.match(html, /href="https:\/\/xincheng213618.github.io\/scgd_general_wpf\/04-api-reference\/algorithms\/detectors\/grid-distortion-v2#outside"/u)
  assert.match(html, /href="https:\/\/xincheng213618.github.io\/scgd_general_wpf\/04-api-reference\/algorithms\/overview"/u)
  assert.doesNotMatch(html, /<script|grid-distortion-demo.css/u, 'Static diagrams do not initialize interactive resources.')
  await assert.rejects(createHelpBundle({ ...inputs, sources: [distortion, distortion] }), /unique/u)
  await assert.rejects(createHelpBundle({ ...inputs, sources: [{ ...distortion, sourcePath: 'docs/../outside.md' }] }), /Invalid.*source path/u)
  await assert.rejects(createHelpBundle({ ...inputs, sources: [{ ...distortion, markdown: distortion.markdown.replace('knowledge_id:', 'other_id:') }] }), /stable knowledge_id/u)
})

test('freshness validation detects both source changes and missing delivery resources', async () => {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'colorvision-algorithm-help-'))
  try {
    const bundle = await createHelpBundle(inputs)
    await writeHelpBundle(bundle, directory)
    await checkHelpBundle(bundle, directory)
    for (const [relative, content] of bundle) await fs.writeFile(path.join(directory, relative), content.replaceAll('\n', '\r\n'))
    await checkHelpBundle(bundle, directory)
    const changed = await createHelpBundle({ ...inputs, demoCss: inputs.demoCss + '\n/* changed */' })
    await assert.rejects(checkHelpBundle(changed, directory), /stale.*index\.json.*grid-distortion-demo\.css/u)
    await fs.unlink(path.join(directory, 'algorithms.grid-distortion.md'))
    await assert.rejects(checkHelpBundle(bundle, directory), /algorithms\.grid-distortion\.md/u)
    await writeHelpBundle(bundle, directory)
    await fs.unlink(path.join(directory, 'boot.js'))
    await assert.rejects(checkHelpBundle(bundle, directory), /boot\.js/u)
  } finally {
    // This directory is a freshly created test-owned absolute path under the system temp folder.
    // Do not invoke recursive removal here: remove only this known flat fixture output.
    for (const entry of await fs.readdir(directory)) await fs.unlink(path.join(directory, entry))
    await fs.rmdir(directory)
  }
})
