import assert from 'node:assert/strict'
import test from 'node:test'
import { effectiveApiKeyStatus, toUtcExpiry } from '../src/utils/apiKeyStatus.ts'
import type { ApiKeyItem } from '../src/types/admin.ts'

function key(overrides: Partial<ApiKeyItem> = {}): ApiKeyItem {
  return {
    id: 1,
    name: 'Test',
    key_prefix: '12345678',
    scopes: 'stats:read',
    is_active: true,
    ...overrides,
  }
}

test('effective API key status distinguishes expiry, revocation, and invalid dates', () => {
  const now = new Date('2030-01-01T00:00:00Z')
  assert.equal(effectiveApiKeyStatus(key(), now), 'active')
  assert.equal(effectiveApiKeyStatus(key({ expires_at: '2030-01-01T00:00:01Z' }), now), 'active')
  assert.equal(effectiveApiKeyStatus(key({ expires_at: '2030-01-01T00:00:00Z' }), now), 'expired')
  assert.equal(effectiveApiKeyStatus(key({ expires_at: 'not-a-date' }), now), 'invalid_expiry')
  assert.equal(effectiveApiKeyStatus(key({ is_active: false }), now), 'revoked')
})

test('server status is authoritative for API key display', () => {
  assert.equal(
    effectiveApiKeyStatus(key({ status: 'expired', expires_at: '2099-01-01T00:00:00Z' })),
    'expired',
  )
})

test('expiry values are converted to explicit UTC timestamps', () => {
  assert.equal(toUtcExpiry(undefined), undefined)
  assert.equal(toUtcExpiry('2030-01-02T03:04:05+08:00'), '2030-01-01T19:04:05.000Z')
  assert.equal(
    toUtcExpiry({ toISOString: () => '2030-01-01T00:00:00.000Z' }),
    '2030-01-01T00:00:00.000Z',
  )
  assert.throws(() => toUtcExpiry('not-a-date'), /过期时间格式无效/)
})
