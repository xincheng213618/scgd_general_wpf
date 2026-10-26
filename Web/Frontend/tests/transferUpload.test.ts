import assert from 'node:assert/strict'
import { createHash, webcrypto } from 'node:crypto'
import test from 'node:test'

import { uploadTransferFile } from '../src/services/transferUpload.ts'

const sampleSize = 64 * 1024
const first = new Uint8Array(sampleSize).fill(0x19)
const last = new Uint8Array(sampleSize).fill(0xe3)
const largeFileSize = Math.round(2.06 * 1024 ** 3)

for (const secureContext of [false, true]) {
  test(`transfer upload preserves SHA-256 fingerprints with Web Crypto ${secureContext ? 'available' : 'unavailable (HTTP)'}`, async (t) => {
    t.mock.getter(globalThis, 'crypto', () => (secureContext ? webcrypto : {
      getRandomValues: webcrypto.getRandomValues.bind(webcrypto),
    }) as Crypto)
    t.mock.getter(globalThis, 'localStorage', () => ({ getItem: () => null, setItem: () => {} }) as unknown as Storage)

    const ranges: [number, number][] = []
    const largeFile = {
      name: 'ColorVision_Diagnostics.zip',
      size: largeFileSize,
      lastModified: 1792972800000,
      slice(start: number, end = largeFileSize) {
        ranges.push([start, end])
        assert.ok(end - start <= sampleSize, 'fingerprinting must not read the whole multi-GB file')
        return new Blob([start === 0 ? first : last])
      },
    } as File
    const small = new Uint8Array([0, 1, 127, 128, 255])
    const fixtures = [
      { file: new File([], 'empty.zip'), first: new Uint8Array(), last: new Uint8Array() },
      { file: new File([small], '小文件.zip', { lastModified: 1792972800000 }), first: small, last: small },
      { file: largeFile, first, last },
    ]

    for (const fixture of fixtures) {
      const { file } = fixture
      const expectedFingerprint = createHash('sha256')
        .update(`${file.name}\u0000${file.size}\u0000${file.lastModified}`)
        .update(fixture.first)
        .update(fixture.last)
        .digest('hex')
      let sessionCreated = false
      t.mock.method(globalThis, 'fetch', async (url, options) => {
        if (url === '/api/auth/session') return Response.json({ csrf_token: 'test-csrf' })
        assert.equal(url, '/api/transfer/uploads')
        assert.equal(options?.method, 'POST')
        assert.deepEqual(JSON.parse(String(options?.body)), {
          filename: file.name,
          total_size: file.size,
          fingerprint: expectedFingerprint,
        })
        const headers = options?.headers as Record<string, string>
        assert.match(headers['X-Transfer-Client'], /^[0-9a-f-]{36}$/i)
        sessionCreated = true
        return Response.json({
          upload_id: 'test-upload',
          name: file.name,
          total_size: file.size,
          offset: file.size,
          complete: true,
          replaced: false,
          download_url: '/api/transfer/files/test',
          share_url: '/transfer/share/test',
        })
      })

      const result = await uploadTransferFile(file)
      assert.equal(sessionCreated, true)
      assert.equal(result.bytes_written, file.size)
    }
    assert.deepEqual(ranges, [[0, sampleSize], [largeFileSize - sampleSize, largeFileSize]])
  })
}
