import { existsSync, readFileSync, statSync } from 'node:fs'
import { resolve } from 'node:path'

const dist = resolve(process.argv[2] || 'dist')
const manifest = JSON.parse(readFileSync(`${dist}/.vite/manifest.json`, 'utf8'))
// Dynamic imports are intentionally explicit roots: this is the JavaScript needed after routing to /admin.
const entryKeys = ['index.html', 'src/layouts/AdminLayout.tsx', 'src/pages/Dashboard.tsx']
const maximumGzipBytes = 450 * 1024

function dependencyClosure(keys, result = new Set()) {
  for (const key of keys) {
    const entry = manifest[key]
    if (!entry) {
      throw new Error(`Bundle manifest is missing ${key}`)
    }
    if (result.has(key)) {
      continue
    }
    result.add(key)
    dependencyClosure(entry.imports || [], result)
  }
  return result
}

// `imports` are static manifest edges. Keep dynamic routes out of the closure unless they are explicit roots.
const files = [...new Set([...dependencyClosure(entryKeys)].map((key) => manifest[key].file))]
const sizes = files.reduce(
  (total, file) => {
    const content = readFileSync(`${dist}/${file}`)
    total.rawBytes += content.length
    const gzipPath = `${dist}/${file}.gz`
    const brotliPath = `${dist}/${file}.br`
    total.gzipBytes += existsSync(gzipPath) ? statSync(gzipPath).size : content.length
    total.brotliBytes += existsSync(brotliPath) ? statSync(brotliPath).size : content.length
    return total
  },
  { rawBytes: 0, gzipBytes: 0, brotliBytes: 0 },
)
const gzipHeadroomBytes = maximumGzipBytes - sizes.gzipBytes
const report = {
  route: '/admin',
  metric: 'precompressed-static-route-closure',
  javascriptRequests: files.length,
  staticBytes: sizes.rawBytes,
  gzipBytes: sizes.gzipBytes,
  brotliBytes: sizes.brotliBytes,
  maximumGzipBytes,
  gzipHeadroomBytes,
  gzipHeadroomPercent: Number(
    ((gzipHeadroomBytes / sizes.gzipBytes) * 100).toFixed(1),
  ),
}

console.log(`Dashboard static route closure: ${JSON.stringify(report)}`)
if (sizes.gzipBytes > maximumGzipBytes) {
  throw new Error(
    `Dashboard static route closure is ${sizes.gzipBytes} gzip bytes; budget is ${maximumGzipBytes}`,
  )
}
