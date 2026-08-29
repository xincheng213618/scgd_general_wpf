import assert from 'node:assert/strict'
import test from 'node:test'
import viteConfig from '../vite.config.ts'

test('the development API proxy preserves the browser origin for CSRF checks', () => {
  const proxy = viteConfig.server?.proxy
  const apiProxy = proxy?.['/api']

  assert.equal(typeof apiProxy, 'object')
  assert.equal(apiProxy?.target, 'http://127.0.0.1:9998')
  assert.equal(apiProxy?.changeOrigin, false)
  assert.equal(proxy?.['/login'], undefined)
  assert.equal(proxy?.['/logout'], undefined)
})
