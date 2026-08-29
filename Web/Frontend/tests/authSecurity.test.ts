import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatLoginRetryCountdown,
  loginLockRemainingSeconds,
  normalizeRetryAfter,
  sessionAfterPasswordChange,
} from '../src/utils/authSecurity.ts'
import { ApiRequestError, authorizationStateNeedsRefresh } from '../src/services/request.ts'

test('login retry values are normalized to safe whole seconds', () => {
  assert.equal(normalizeRetryAfter('59.2'), 60)
  assert.equal(normalizeRetryAfter(-1), 0)
  assert.equal(normalizeRetryAfter('invalid'), 0)
  assert.equal(normalizeRetryAfter(90_000), 86_400)
})

test('login lock countdown stays compact and unambiguous', () => {
  assert.equal(formatLoginRetryCountdown(0), '00:00')
  assert.equal(formatLoginRetryCountdown(65), '01:05')
  assert.equal(formatLoginRetryCountdown(3661), '01:01:01')
})

test('API errors retain structured throttle metadata', () => {
  const error = new ApiRequestError('登录尝试过于频繁', 429, {
    retry_after: 900,
    attempts_remaining: 0,
  })
  assert.equal(error.status, 429)
  assert.equal(error.retryAfter, 900)
  assert.equal(error.attemptsRemaining, 0)
})

test('only authorization-state failures request a live session refresh', () => {
  assert.equal(authorizationStateNeedsRefresh(403, { code: 'insufficient_scope' }), true)
  assert.equal(authorizationStateNeedsRefresh(403, { code: 'password_change_required' }), true)
  assert.equal(authorizationStateNeedsRefresh(403, { code: 'permission_revision_conflict' }), false)
  assert.equal(authorizationStateNeedsRefresh(401, { code: 'insufficient_scope' }), false)
  assert.equal(authorizationStateNeedsRefresh(403, 'Insufficient scope'), false)
})

test('admin lock countdown is derived from the absolute unlock time', () => {
  const now = Date.parse('2026-08-29T08:00:00Z')
  assert.equal(loginLockRemainingSeconds('2026-08-29T08:01:05Z', now), 65)
  assert.equal(loginLockRemainingSeconds('2026-08-29T07:59:00Z', now), 0)
  assert.equal(loginLockRemainingSeconds('invalid', now), 0)
})

test('password change updates the session gate without dropping authenticated state', () => {
  const session = sessionAfterPasswordChange({
    authenticated: true,
    username: 'operator',
    csrf_token: 'csrf-token',
    must_change_password: true,
    can_access_admin: true,
    permissions: ['admin:access', 'users:read'],
  }, { must_change_password: false })

  assert.equal(session.authenticated, true)
  assert.equal(session.username, 'operator')
  assert.equal(session.csrf_token, 'csrf-token')
  assert.equal(session.must_change_password, false)
  assert.deepEqual(session.permissions, ['admin:access', 'users:read'])
})
