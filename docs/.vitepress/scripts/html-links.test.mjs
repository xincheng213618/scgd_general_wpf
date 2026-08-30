import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { parseHtmlLinks, hasHtmlAnchor, validateBuiltLinkFragments } from './html-links.mjs'

const basePath = '/scgd_general_wpf/'
const temporaryPrefix = 'colorvision-html-links-test-'

function html(body) {
  return `<!doctype html><html><head><title>Link fixture</title></head><body>${body}</body></html>`
}

async function fixture(t, files) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), temporaryPrefix))
  t.after(async () => {
    // Delete only this test's concrete mkdtemp directory, never a supplied root.
    const actual = await fs.realpath(root)
    const temporaryRoot = await fs.realpath(os.tmpdir())
    assert.equal(path.dirname(actual), temporaryRoot)
    assert.ok(path.basename(actual).startsWith(temporaryPrefix))
    await fs.rm(actual, { recursive: true, force: true })
  })

  const distRoot = path.join(root, 'dist')
  await fs.mkdir(distRoot)
  for (const [relativePath, content] of Object.entries(files)) {
    const target = path.resolve(distRoot, relativePath)
    const relative = path.relative(distRoot, target)
    assert.ok(relative && !relative.startsWith('..') && !path.isAbsolute(relative))
    await fs.mkdir(path.dirname(target), { recursive: true })
    await fs.writeFile(target, content, 'utf8')
  }
  return { root, distRoot }
}

async function validate(distRoot, siteBasePath = basePath) {
  const result = await validateBuiltLinkFragments(distRoot, { basePath: siteBasePath })
  assert.equal(typeof result.htmlFiles, 'number')
  assert.equal(typeof result.checkedLinks, 'number')
  assert.ok(Array.isArray(result.failures))
  return result
}

function assertFailure(result, { source, href, category }) {
  const matching = result.failures.filter((failure) => {
    const message = String(failure).replace(/\\/gu, '/')
    return message.includes(source) && category.test(message)
  })
  const fragment = href.includes('#') ? href.slice(href.indexOf('#') + 1) : ''
  assert.ok(matching.some((failure) => failure.includes(href) || (fragment && failure.includes(fragment))),
    `Expected ${category} with source ${source} and href/fragment ${href}; got ${JSON.stringify(result.failures)}`)
}

test('HTML links come from real attributes, not comments, raw script/style text or encoded examples', () => {
  const parsed = parseHtmlLinks(html(`
    <!-- <a id="comment-ghost" href="#comment-ghost">example</a> -->
    <script>const example = '<a id="script-ghost" href="#script-ghost">';</script>
    <style>.example::after { content: '<a id="style-ghost" href="#style-ghost">'; }</style>
    <pre><code>&lt;a id="code-ghost" href="#code-ghost"&gt;</code></pre>
    <h2 ID = "CaseSensitive">Heading</h2>
    <div id='single-quoted'></div><div id=unquoted></div>
    <a HREF = "#CaseSensitive">real link</a>
  `))

  assert.ok(parsed.ids instanceof Set)
  assert.deepEqual([...parsed.ids].sort(), ['CaseSensitive', 'single-quoted', 'unquoted'].sort())
  assert.deepEqual(parsed.hrefs, ['#CaseSensitive'])
})

test('literal Vue interpolation delimiters, including an unclosed pair, do not swallow HTML nodes', () => {
  for (const body of [
    '{{ <a id="visible" href="#visible">real HTML</a> }}',
    '{{ unclosed text <a id="visible" href="#visible">real HTML</a>',
  ]) {
    const parsed = parseHtmlLinks(html(body))
    assert.deepEqual([...parsed.ids], ['visible'])
    assert.deepEqual(parsed.hrefs, ['#visible'])
  }
})

test('template contents are inert while the template element itself keeps its ID', () => {
  const content = html('<template id="template-root"><div id="template-child"></div><a href="#inert-missing">inert</a></template><a id="real" href="#real">real</a>')
  const parsed = parseHtmlLinks(content)
  assert.deepEqual([...parsed.ids].sort(), ['real', 'template-root'])
  assert.deepEqual(parsed.hrefs, ['#real'])
  assert.equal(hasHtmlAnchor(content, 'template-root'), true)
  assert.equal(hasHtmlAnchor(content, 'template-child'), false)
})

test('real elements inside pre and code remain nodes while encoded examples remain text', () => {
  const parsed = parseHtmlLinks(html('<pre><code><a id="live-code" href="#live-code">real node</a>&lt;a id="example-only" href="#example-only"&gt;</code></pre>'))
  assert.deepEqual([...parsed.ids], ['live-code'])
  assert.deepEqual(parsed.hrefs, ['#live-code'])
})

test('duplicate ID and href attributes use the first value, including differently cased names', () => {
  const parsed = parseHtmlLinks(html('<a id="first" id="second" ID="third" href="#first" href="#second" HREF="#third">first wins</a>'))
  assert.deepEqual([...parsed.ids], ['first'])
  assert.deepEqual(parsed.hrefs, ['#first'])
})

test('HTML parse failures other than duplicate attributes are not silently swallowed', async (t) => {
  const malformed = html('<div><span></div>')
  assert.throws(() => parseHtmlLinks(malformed))
  const { distRoot } = await fixture(t, { 'broken.html': malformed })
  const result = await validate(distRoot)
  assert.ok(result.failures.some((failure) => failure.includes('broken.html') && /invalid/iu.test(failure)),
    `Expected a source-specific parse error, got ${JSON.stringify(result.failures)}`)
})

test('anchor comparison preserves case, whitespace, Unicode punctuation and normalization form', () => {
  const content = html('<h2 id="备份、执行与失败分层"></h2><div id="Case"></div><div id=" spaced "></div><div id="Café"></div>')
  assert.equal(hasHtmlAnchor(content, '备份、执行与失败分层'), true)
  assert.equal(hasHtmlAnchor(content, encodeURIComponent('备份、执行与失败分层')), true)
  assert.equal(hasHtmlAnchor(content, '备份执行与失败分层'), false)
  assert.equal(hasHtmlAnchor(content, 'Case'), true)
  assert.equal(hasHtmlAnchor(content, 'case'), false)
  assert.equal(hasHtmlAnchor(content, '%20spaced%20'), true)
  assert.equal(hasHtmlAnchor(content, 'spaced'), false)
  assert.equal(hasHtmlAnchor(content, encodeURIComponent('Cafe\u0301')), false)
})

test('HTML attribute entities are decoded without converting NBSP or zero-width space into ordinary text', () => {
  const content = html('<div id="a&amp;b"></div><div id="a&nbsp;b"></div><div id="a&copy;b"></div><div id="a&ZeroWidthSpace;b"></div><a href="#a&amp;b">link</a>')
  const parsed = parseHtmlLinks(content)
  assert.ok(parsed.ids.has('a&b'))
  assert.ok(parsed.ids.has('a\u00a0b'))
  assert.ok(parsed.ids.has('a©b'))
  assert.ok(parsed.ids.has('a\u200bb'))
  assert.deepEqual(parsed.hrefs, ['#a&b'])
  assert.equal(hasHtmlAnchor(content, 'a%26b'), true)
  assert.equal(hasHtmlAnchor(content, 'a%C2%A0b'), true)
  assert.equal(hasHtmlAnchor(content, 'a%20b'), false)
  assert.equal(hasHtmlAnchor(content, 'a%C2%A9b'), true)
  assert.equal(hasHtmlAnchor(content, 'a%E2%80%8Bb'), true)
})

test('fragments are percent-decoded exactly once and malformed encodings do not throw', () => {
  const content = html('<div id="a%2Fb"></div><div id="literal:~:value"></div>')
  assert.equal(hasHtmlAnchor(content, 'a%252Fb'), true)
  assert.equal(hasHtmlAnchor(content, 'a%2Fb'), false)
  assert.equal(hasHtmlAnchor(content, 'literal%3A~%3Avalue'), true)
  for (const fragment of ['%', '%ZZ', '%E0%A4%A']) {
    assert.equal(hasHtmlAnchor(content, fragment), false)
  }
  const invalidEntity = html('<div id="&#x110000;"></div>')
  assert.doesNotThrow(() => parseHtmlLinks(invalidEntity))
  assert.doesNotThrow(() => hasHtmlAnchor(invalidEntity, '%EF%BF%BD'))
})

test('complete site validation accepts same-page, clean, explicit HTML, directory and base-prefixed routes', async (t) => {
  const { distRoot } = await fixture(t, {
    'index.html': html('<h1 id="首页"></h1><a href="#首页">home</a><a href="/scgd_general_wpf/?q=x#首页">base root query</a>'),
    'guide/source.html': html(`
      <h1 id="local"></h1>
      <a href="#local">same page</a><a href="?q=x#local">same page query</a>
      <a href="../target#target">relative clean</a><a href="../target.html#target">relative HTML</a>
      <a href="/scgd_general_wpf/target#target">base clean</a>
      <a href="/scgd_general_wpf/target.html?x=1&amp;y=2#target">base HTML query</a>
      <a href="../section/#section-home">directory</a><a href="../section/index.html#section-home">index HTML</a>
      <a href="../ColorVision.Common#api">dotted clean route</a>
      <a href="../%E4%B8%AD%E6%96%87%E9%A1%B5#%E4%B8%AD%E6%96%87%E3%80%81%E6%A0%87%E7%82%B9">encoded path and fragment</a>
      <a href="../target#choice?flag">question mark belongs to fragment</a>
      <a href="../target#part#tail">second hash belongs to fragment</a>
    `),
    'target.html': html('<h1 id="target"></h1><div id="choice?flag"></div><div id="part#tail"></div>'),
    'section/index.html': html('<h1 id="section-home"></h1>'),
    'ColorVision.Common.html': html('<h1 id="api"></h1>'),
    '中文页.html': html('<h1 id="中文、标点"></h1>'),
  })
  const result = await validate(distRoot)
  assert.deepEqual(result.failures, [])
  assert.ok(result.htmlFiles > 0)
  assert.ok(result.checkedLinks > 0)
})

test('a site actually deployed at the root accepts bare-root clean, HTML and directory routes', async (t) => {
  const { distRoot } = await fixture(t, {
    'index.html': html('<h1 id="home"></h1><a href="/?mode=x#home">root query</a><a href="/target#target">clean</a><a href="/target.html?mode=x#target">HTML</a><a href="/section/#section-home">directory</a>'),
    'target.html': html('<h1 id="target"></h1>'),
    'section/index.html': html('<h1 id="section-home"></h1>'),
  })
  assert.deepEqual((await validate(distRoot, '/')).failures, [])
})

test('dot-segment directory links resolve to index HTML rather than a same-named clean page', async (t) => {
  const { distRoot } = await fixture(t, {
    'index.html': html('<h1 id="parent-directory"></h1>'),
    'guide.html': html('<h1 id="wrong-clean-page"></h1>'),
    'guide/index.html': html('<h1 id="right-directory"></h1>'),
    'guide/folder/index.html': html('<h1 id="unused-child"></h1>'),
    'guide/source.html': html(`
      <a href=".#right-directory">dot</a><a href="./#right-directory">dot slash</a>
      <a href="..#parent-directory">parent</a><a href="../#parent-directory">parent slash</a>
      <a href="folder/..#right-directory">child then parent</a><a href="folder/../#right-directory">child then parent slash</a>
      <a href="%2e#right-directory">encoded dot</a><a href="%2e%2e#parent-directory">encoded parent</a>
      <a href="folder/%2e%2e#right-directory">child then encoded parent</a>
    `),
  })
  assert.deepEqual((await validate(distRoot)).failures, [])
})

test('empty fragments, query-only links, external URLs and non-HTML resource fragments do not require HTML IDs', async (t) => {
  const { distRoot } = await fixture(t, {
    'index.html': html(`
      <a href="">empty</a><a href="#">top</a><a href="?mode=x">query only</a><a href="?mode=x#">empty fragment</a>
      <a href="https://example.invalid/absent#missing">HTTPS</a><a href="//example.invalid/absent#missing">network path</a>
      <a href="mailto:fixture@example.invalid#missing">mail</a><a href="tel:+10000000000#missing">phone</a>
      <a href="app://fixture/missing#anchor">application scheme</a><a href="data:text/plain,fixture#missing">data</a>
      <a href="assets/picture.svg#symbol-not-an-html-id">SVG</a><a href="assets/manual.pdf#page=2">PDF</a>
      <a href="assets/download.zip#member">archive</a>
    `),
    'assets/picture.svg': '<svg xmlns="http://www.w3.org/2000/svg"></svg>',
    'assets/manual.pdf': 'Not a real PDF: the validator must not interpret this fixture as HTML.',
    'assets/download.zip': 'Not a real archive: existence only, no extraction.',
  })
  assert.deepEqual((await validate(distRoot)).failures, [])
})

test('browser top fragments and pure text directives need no ID, while elements before directives are resolved', async (t) => {
  const { distRoot } = await fixture(t, {
    'index.html': html(`
      <h1 id="heading"></h1>
      <a href="#top">top</a><a href="#TOP">case-insensitive top fallback</a>
      <a href="#heading:~:text=selection">same-page element and text</a>
      <a href="#:~:text=selection">same-page text only</a>
      <a href="target#top">cross-page top</a><a href="target#:~:text=selection">cross-page text only</a>
      <a href="target#heading:~:text=selection">cross-page element and text</a>
      <a href="target#literal%3A~%3Avalue">encoded delimiter is part of the ID</a>
      <a href="target#literal%3A~%3Avalue:~:text=selection">encoded ID before a real directive</a>
    `),
    'target.html': html('<h1 id="heading"></h1><div id="literal:~:value"></div>'),
  })
  assert.deepEqual((await validate(distRoot)).failures, [])
})

const invalidLinkCases = [
  { name: 'same-page missing anchor', href: '#missing-local', category: /missing anchor/iu },
  { name: 'cross-page missing anchor', href: '../target#missing-remote', category: /missing anchor/iu },
  { name: 'dotted clean route still checks anchors', href: '../ColorVision.Common#missing-dotted', category: /missing anchor/iu },
  { name: 'missing page', href: '../absent-page#missing-page-anchor', category: /missing target/iu },
  { name: 'invalid fragment encoding', href: '../target#%ZZ', category: /invalid/iu },
  { name: 'invalid UTF-8 fragment', href: '../target#%E0%A4%A', category: /invalid/iu },
  { name: 'invalid path encoding', href: '../bad%ZZ#path-encoding', category: /invalid/iu },
  { name: 'missing element before a text directive', href: '../target#missing-before-directive:~:text=selection', category: /missing anchor/iu },
  { name: 'missing page with a pure text directive', href: '../absent-page#:~:text=selection', category: /missing target/iu },
  { name: 'encoded directive delimiter must not match only an ID prefix', href: '../target#literal%3A~%3Amissing', category: /missing anchor/iu },
  { name: 'bare-root clean route outside the deployment base', href: '/target#target', category: /outside.*base|escapes/iu },
  { name: 'bare-root HTML route outside the deployment base', href: '/target.html#target', category: /outside.*base|escapes/iu },
  { name: 'similar prefix outside the deployment base', href: '/scgd_general_wpfish/target#target', category: /outside.*base|escapes/iu },
]

for (const scenario of invalidLinkCases) {
  test(`complete validation diagnoses ${scenario.name} with its source and link`, async (t) => {
    const { distRoot } = await fixture(t, {
      'guide/source.html': html(`<h1 id="local"></h1><a href="${scenario.href}">broken link</a>`),
      'target.html': html('<h1 id="target"></h1><div id="literal"></div>'),
      'ColorVision.Common.html': html('<h1 id="api"></h1>'),
      'scgd_general_wpfish/target.html': html('<h1 id="target"></h1>'),
    })
    assertFailure(await validate(distRoot), { source: 'guide/source.html', ...scenario })
  })
}

test('compatibility-page body links are checked without interpreting JavaScript navigation as an HTML link', async (t) => {
  const href = '../target#missing-compatibility-anchor'
  const { distRoot } = await fixture(t, {
    'legacy/old.html': html(`<script>window.location.replace('../target')</script><a href="${href}">new page</a>`),
    'target.html': html('<h1 id="target"></h1>'),
  })
  assertFailure(await validate(distRoot), { source: 'legacy/old.html', href, category: /missing anchor/iu })
})

for (const href of ['../../outside/foreign#protected', '%2e%2e/%2e%2e/outside/foreign#protected']) {
  test(`path containment rejects escape ${href}`, async (t) => {
    const { root, distRoot } = await fixture(t, {
      'guide/source.html': html(`<a href="${href}">outside</a>`),
    })
    await fs.mkdir(path.join(root, 'outside'))
    await fs.writeFile(path.join(root, 'outside/foreign.html'), html('<h1 id="protected"></h1>'), 'utf8')
    assertFailure(await validate(distRoot), { source: 'guide/source.html', href, category: /escapes/iu })
  })
}

test('realpath containment rejects a directory symlink escaping the published root', async (t) => {
  const href = 'escape/foreign#protected'
  const { root, distRoot } = await fixture(t, {
    'index.html': html(`<a href="${href}">outside through symlink</a>`),
  })
  const outside = path.join(root, 'outside')
  await fs.mkdir(outside)
  await fs.writeFile(path.join(outside, 'foreign.html'), html('<h1 id="protected"></h1>'), 'utf8')
  const link = path.join(distRoot, 'escape')
  try {
    await fs.symlink(outside, link, process.platform === 'win32' ? 'junction' : 'dir')
  } catch (error) {
    if (['EPERM', 'EACCES', 'ENOTSUP'].includes(error.code)) {
      t.skip('Host cannot create the isolated test symlink')
      return
    }
    throw error
  }
  try {
    assertFailure(await validate(distRoot), { source: 'index.html', href, category: /escapes/iu })
  } finally {
    // Remove the link itself before the guarded fixture cleanup; do not traverse its target.
    await fs.unlink(link)
  }
})
