import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import { scanReadmeLinks, validateReadmeDocsLinks } from './readme-links.mjs'

async function write(root, relative, text = '') {
  const target = path.join(root, relative)
  await fs.mkdir(path.dirname(target), { recursive: true })
  await fs.writeFile(target, text)
}

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'colorvision-readme-links-test-'))
  t.after(async () => {
    const actual = await fs.realpath(root)
    assert.equal(path.dirname(actual), await fs.realpath(os.tmpdir()))
    assert.ok(path.basename(actual).startsWith('colorvision-readme-links-test-'))
    await fs.rm(actual, { recursive: true, force: true })
  })
  await write(root, 'docs/topic.md', '# Topic\n')
  return root
}

test('scanner reads common inline, reference and HTML links with useful line numbers', () => {
  const links = scanReadmeLinks([
    '[plain](../docs/topic.md#section "A title")',
    '[angle](<../docs/space name.md>)',
    '[parens](../docs/part(one).md)',
    '[named][Doc Ref] and [collapsed][] and [shortcut]',
    '[Doc Ref]: ../docs/reference.md "Title"',
    '[collapsed]: <../docs/collapsed name.md>',
    '[shortcut]: ../docs/short.md',
    '<a class="doc" href="../docs/html.md?x=1&amp;y=2">Read</a>',
  ].join('\n'))
  assert.deepEqual(links, [
    { target: '../docs/topic.md#section', line: 1 },
    { target: '../docs/space name.md', line: 2 },
    { target: '../docs/part(one).md', line: 3 },
    { target: '../docs/reference.md', line: 4 },
    { target: '../docs/collapsed name.md', line: 4 },
    { target: '../docs/short.md', line: 4 },
    { target: '../docs/html.md?x=1&y=2', line: 8 },
  ])
})

test('scanner ignores frontmatter, code fences, inline code, comments and escaped links', () => {
  const markdown = [
    '---', 'description: "[fake](../docs/frontmatter.md)"', '---',
    '```markdown', '[fake](../docs/fenced.md)', '```',
    '~~~~', '```', '[fake](../docs/nested-fence.md)', '~~~~',
    '`[fake](../docs/inline.md)` and ``<a href="../docs/code.md">``',
    '<!-- [fake](../docs/comment.md)', '<a href="../docs/comment2.md"> -->',
    '\\[fake](../docs/escaped.md)', '[real](../docs/topic.md)',
  ].join('\n')
  assert.deepEqual(scanReadmeLinks(markdown), [{ target: '../docs/topic.md', line: 15 }])
})

test('code and comments are masked in source order; escaped backticks do not open code', () => {
  for (const markdown of [
    '```html\n<!-- unfinished\n```\n[real](docs/missing.md)',
    '`<!-- unfinished`\n[real](docs/missing.md)',
    '<!--\n```\n-->\n[real](docs/missing.md)',
    '<!-- ` -->\n[real](docs/missing.md)',
    '\\` [real](docs/missing.md) \\`',
  ]) {
    assert.deepEqual(scanReadmeLinks(markdown).map((link) => link.target), ['docs/missing.md'])
  }
})

test('HTML tags respect quoted greater-than signs and href/src text inside other attributes', () => {
  const markdown = [
    '<a title="1 > 0" href="docs/missing.md">real</a>',
    '<a title="x href=\'docs/fake.md\'" href="docs/topic.md">real</a>',
    '<img data-src="docs/fake.png" title="src=\'docs/fake2.png\'" src="docs/real.png">',
    '<a data-href="docs/fake.md">not a link</a>',
  ].join('\n')
  assert.deepEqual(scanReadmeLinks(markdown), [
    { target: 'docs/missing.md', line: 1 },
    { target: 'docs/topic.md', line: 2 },
    { target: 'docs/real.png', line: 3 },
  ])
})

test('discovers new READMEs without Git and keeps them outside the topic model', async (t) => {
  const root = await fixture(t)
  await write(root, 'README.md', '[doc](docs/topic.md)\n[source](missing-source.cs)\n[web](https://example.com/docs/missing.md)\n')
  await write(root, 'SDK/NewModule/README.md', '[doc](../../docs/topic.md)\n')
  await write(root, 'Drivers/NewDriver/readme.md', '[doc](/docs/topic.md)\n')
  await write(root, 'AndroidWebViewApp/README.md', '# Source readme without knowledge metadata\n')
  assert.deepEqual(await validateReadmeDocsLinks(root), { readmes: 4, links: 3 })
})

test('accepts encoded names, clean/html URLs, directory entries and non-searchable maps', async (t) => {
  const root = await fixture(t)
  await write(root, 'docs/space name.md', '# Spaced\n')
  await write(root, 'docs/ColorVision.Common.md', '# Dotted page\n')
  await write(root, 'docs/planned.md', '---\nstatus: planned\n---\n# Planned\n')
  await write(root, 'docs/knowledge/index.md', '---\nsearch: false\n---\n# Generated map\n')
  await write(root, 'docs/area/README.md', '# Directory\n')
  await write(root, 'README.md', [
    '[encoded](docs/space%20name.md)', '[angle](<docs/space name.md>)',
    '[clean](docs/topic)', '[dotted](docs/ColorVision.Common)', '[html](docs/topic.html)',
    '[directory](docs/area/)', '[map](/docs/knowledge/)', '[planned](docs/planned.md#not-checked)',
  ].join('\n'))
  assert.deepEqual(await validateReadmeDocsLinks(root), { readmes: 1, links: 8 })
})

test('aggregates missing pages and retired explicit, clean, html and directory targets', async (t) => {
  const root = await fixture(t)
  const redirect = '---\nsearch: false\nredirect_from_deleted_page: true\n---\n# Old page\n'
  await write(root, 'docs/old.md', redirect)
  await write(root, 'docs/retired/README.md', redirect)
  await write(root, 'README.md', [
    '[missing](docs/missing.md)', '[old](docs/old.md)', '[clean](docs/old)',
    '[html](docs/old.html)', '[directory](docs/retired/)',
  ].join('\n'))
  await assert.rejects(validateReadmeDocsLinks(root), (error) => {
    assert.match(error.message, /5 failure\(s\)/u)
    assert.match(error.message, /README.md:1: missing documentation/u)
    assert.equal((error.message.match(/retired redirect compatibility page/gu) ?? []).length, 4)
    return true
  })
})

test('explicit directory URLs use directory entries, never a same-name Markdown file', async (t) => {
  const root = await fixture(t)
  await write(root, 'docs/area.md', '# Not the directory entry\n')
  await write(root, 'docs/area/README.md', '---\nredirect_from_deleted_page: true\n---\n# Retired entry\n')
  await write(root, 'docs/empty.md', '# Does not satisfy a directory URL\n')
  await fs.mkdir(path.join(root, 'docs/empty'))
  await write(root, 'README.md', '[slash](docs/area/)\n[dot](docs/area/.)\n[parent](docs/area/child/..)\n[empty](docs/empty/)\n')
  await assert.rejects(validateReadmeDocsLinks(root), (error) => {
    assert.match(error.message, /4 failure\(s\)/u)
    assert.equal((error.message.match(/retired redirect compatibility page/gu) ?? []).length, 3)
    assert.match(error.message, /missing documentation page or directory entry: docs\/empty/u)
    return true
  })
})

test('Markdown images and quoted HTML href/src resources get path checks, not frontmatter parsing', async (t) => {
  const root = await fixture(t)
  await write(root, 'docs/image.png', Buffer.from([0xff, 0x00, 0x80]))
  await write(root, 'docs/manual.pdf', '---\nredirect_from_deleted_page: true\n---\nNot Markdown\n')
  await write(root, 'README.md', '![image](docs/image.png)\n<a href="docs/manual.pdf">PDF</a>\n<img src="/docs/image.png" data-href="/docs/missing.md">\n')
  assert.deepEqual(await validateReadmeDocsLinks(root), { readmes: 1, links: 3 })
  await write(root, 'README.md', '<img src="docs/missing.png">\n')
  await assert.rejects(validateReadmeDocsLinks(root), /missing documentation page/u)
})

test('explicit discovery exclusions are applied before reading their READMEs', async (t) => {
  const root = await fixture(t)
  for (const folder of ['.hidden', 'docs', 'bin', 'OBJ', 'node_modules', 'packages', 'vendor', 'third_party', 'third-party',
    'artifacts', 'TestResults', 'log', 'logs', 'Release', 'Debug', 'x64', 'x86', 'DLL']) {
    await write(root, `${folder}/README.md`, '[bad](/docs/missing.md)\n')
  }
  await write(root, 'Other/DLL/README.md', '[good](/docs/topic.md)\n')
  await write(root, 'NewModule/README.md', '[good](/docs/topic.md)\n')
  assert.deepEqual(await validateReadmeDocsLinks(root), { readmes: 2, links: 2 })
})

test('rejects wrong casing and malformed docs paths while ignoring out-of-scope source paths', async (t) => {
  const root = await fixture(t)
  await write(root, 'README.md', '[wrong](docs/Topic.md)\n[encoding](docs/bad%XX.md)\n[nul](docs/bad%00.md)\n')
  await assert.rejects(validateReadmeDocsLinks(root), /3 failure\(s\)/u)
  await write(root, 'README.md', '[source](outside.cs)\n[traversal](../outside.md)\n[local](docs/../source.cs)\n[site](/knowledge/)\n')
  assert.deepEqual(await validateReadmeDocsLinks(root), { readmes: 1, links: 0 })
})

test('does not follow directory junctions and rejects docs realpaths escaping docs', async (t) => {
  const root = await fixture(t)
  const outside = await fixture(t)
  await write(outside, 'README.md', '[bad](/docs/missing.md)\n')
  await write(root, 'source-target/topic.md', '# Not a docs page\n')
  try {
    await fs.symlink(outside, path.join(root, 'linked-tree'), 'junction')
    await fs.symlink(path.join(root, 'source-target'), path.join(root, 'docs/escape'), 'junction')
  } catch (error) {
    if (['EPERM', 'EACCES', 'ENOTSUP'].includes(error.code)) { t.skip('Host cannot create directory links'); return }
    throw error
  }
  await write(root, 'README.md', '[good](docs/topic.md)\n')
  assert.deepEqual(await validateReadmeDocsLinks(root), { readmes: 1, links: 1 })
  await write(root, 'README.md', '[escape](docs/escape/topic.md)\n')
  await assert.rejects(validateReadmeDocsLinks(root), /escapes docs/u)
})

test('rejects a source README symlink instead of reading its content', async (t) => {
  const root = await fixture(t)
  await write(root, 'source-target.md', '[must not read](/docs/missing.md)\n')
  try { await fs.symlink(path.join(root, 'source-target.md'), path.join(root, 'README.md'), 'file') } catch (error) {
    if (['EPERM', 'EACCES', 'ENOTSUP'].includes(error.code)) { t.skip('Host cannot create file symlinks'); return }
    throw error
  }
  await assert.rejects(validateReadmeDocsLinks(root), (error) => {
    assert.match(error.message, /source README symlink is not read/u)
    assert.doesNotMatch(error.message, /missing documentation/u)
    return true
  })
})
