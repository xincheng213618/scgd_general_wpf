import assert from 'node:assert/strict'
import test from 'node:test'
import { sessionAddressLabel, sessionClientLabel } from '../src/utils/accountSessions.ts'

test('session client labels recognize common browsers and platforms', () => {
  assert.equal(
    sessionClientLabel('Mozilla/5.0 (Windows NT 10.0) Chrome/140.0 Safari/537.36 Edg/140.0'),
    'Microsoft Edge · Windows',
  )
  assert.equal(
    sessionClientLabel('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0) AppleWebKit/605.1 Safari/604.1'),
    'Safari · iOS/iPadOS',
  )
})

test('session labels keep safe fallbacks for unavailable metadata', () => {
  assert.equal(sessionClientLabel('custom-agent'), '未知客户端')
  assert.equal(sessionAddressLabel(''), '未知地址')
  assert.equal(sessionAddressLabel('  10.0.0.8  '), '10.0.0.8')
})
