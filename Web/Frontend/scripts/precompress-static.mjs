import {
  brotliCompressSync,
  brotliDecompressSync,
  constants,
  gunzipSync,
  gzipSync,
} from 'node:zlib'
import { readdirSync, readFileSync, statSync, unlinkSync, utimesSync, writeFileSync } from 'node:fs'
import { extname, join, relative, resolve } from 'node:path'

const dist = resolve(process.argv[2] || 'dist')
const compressibleExtensions = new Set(['.css', '.html', '.js', '.json', '.svg'])
const encodings = [
  {
    extension: '.gz',
    compress: (content) => gzipSync(content, { level: 9 }),
    decompress: gunzipSync,
  },
  {
    extension: '.br',
    compress: (content) => brotliCompressSync(content, {
      params: { [constants.BROTLI_PARAM_QUALITY]: 11 },
    }),
    decompress: brotliDecompressSync,
  },
]

function filesUnder(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)
    if (entry.isDirectory()) {
      return entry.name === '.vite' ? [] : filesUnder(path)
    }
    return entry.isFile() ? [path] : []
  })
}

let sourceBytes = 0
let gzipVariantBytes = 0
let brotliVariantBytes = 0
let variantCount = 0

for (const sourcePath of filesUnder(dist)) {
  if (!compressibleExtensions.has(extname(sourcePath))) {
    continue
  }
  const content = readFileSync(sourcePath)
  const sourceStat = statSync(sourcePath)
  sourceBytes += content.length

  for (const encoding of encodings) {
    const variantPath = `${sourcePath}${encoding.extension}`
    const compressed = encoding.compress(content)
    if (compressed.length >= content.length) {
      try {
        unlinkSync(variantPath)
      } catch (error) {
        if (error.code !== 'ENOENT') throw error
      }
      continue
    }
    writeFileSync(variantPath, compressed)
    utimesSync(variantPath, sourceStat.atime, sourceStat.mtime)
    const writtenVariant = readFileSync(variantPath)
    if (!encoding.decompress(writtenVariant).equals(content)) {
      throw new Error(`Precompressed output verification failed: ${variantPath}`)
    }
    variantCount += 1
    if (encoding.extension === '.gz') gzipVariantBytes += compressed.length
    if (encoding.extension === '.br') brotliVariantBytes += compressed.length
  }
}

console.log(`Static precompression: ${JSON.stringify({
  dist: relative(process.cwd(), dist) || '.',
  sourceBytes,
  gzipVariantBytes,
  brotliVariantBytes,
  variantCount,
})}`)
