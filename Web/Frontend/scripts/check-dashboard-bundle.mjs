import { readFileSync } from 'node:fs'
import { gzipSync } from 'node:zlib'

const manifest = JSON.parse(readFileSync('dist/.vite/manifest.json', 'utf8'))
// Dynamic imports are intentionally explicit roots: this is the JavaScript needed after routing to /admin.
const entryKeys = ['index.html', 'src/layouts/AdminLayout.tsx', 'src/pages/Dashboard.tsx']
const maximumSyntheticGzipBytes = 450 * 1024

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
    const content = readFileSync(`dist/${file}`)
    total.rawBytes += content.length
    // This independently compresses every static file. It estimates potential gzip transfer bytes,
    // not actual Flask response bytes: the local backend does not serve Content-Encoding: gzip.
    total.syntheticGzipBytes += gzipSync(content).length
    return total
  },
  { rawBytes: 0, syntheticGzipBytes: 0 },
)
const syntheticGzipHeadroomBytes = maximumSyntheticGzipBytes - sizes.syntheticGzipBytes
const report = {
  route: '/admin',
  metric: 'estimated-static-route-closure',
  javascriptRequests: files.length,
  staticBytes: sizes.rawBytes,
  syntheticGzipBytes: sizes.syntheticGzipBytes,
  maximumSyntheticGzipBytes,
  syntheticGzipHeadroomBytes,
  syntheticGzipHeadroomPercent: Number(
    ((syntheticGzipHeadroomBytes / sizes.syntheticGzipBytes) * 100).toFixed(1),
  ),
}

console.log(`Dashboard static route closure estimate: ${JSON.stringify(report)}`)
if (sizes.syntheticGzipBytes > maximumSyntheticGzipBytes) {
  throw new Error(
    `Dashboard static route closure estimate is ${sizes.syntheticGzipBytes} synthetic gzip bytes; budget is ${maximumSyntheticGzipBytes}`,
  )
}
