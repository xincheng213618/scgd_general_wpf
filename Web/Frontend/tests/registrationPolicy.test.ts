import assert from 'node:assert/strict'
import test from 'node:test'
import {
  authenticatedEntryRedirect,
  authEntryDescription,
  publicAuthEntryLabel,
  REGISTRATION_WELCOME_PATH,
  resolveAuthEntryMode,
  shouldShowRegistrationWelcome,
  shouldShowRegistrationDisabledNotice,
} from '../src/utils/registrationPolicy.ts'

test('registration requests fail closed when public registration is disabled', () => {
  assert.equal(resolveAuthEntryMode('register', false), 'login')
  assert.equal(resolveAuthEntryMode('register', true), 'register')
  assert.equal(resolveAuthEntryMode('login', true), 'login')
  assert.equal(resolveAuthEntryMode(null, true), 'login')
})

test('public navigation names only the capabilities currently available', () => {
  assert.equal(publicAuthEntryLabel(false), '登录')
  assert.equal(publicAuthEntryLabel(true), '登录 / 注册')
})

test('the auth entry explains the live registration policy without alarming normal login', () => {
  assert.match(authEntryDescription(true), /注册账号/)
  assert.match(authEntryDescription(false), /管理员创建/)
  assert.equal(shouldShowRegistrationDisabledNotice('register', false), true)
  assert.equal(shouldShowRegistrationDisabledNotice('login', false), false)
  assert.equal(shouldShowRegistrationDisabledNotice(null, false), false)
  assert.equal(shouldShowRegistrationDisabledNotice('register', true), false)
})

test('successful registration uses a one-time personal-center welcome marker', () => {
  assert.equal(REGISTRATION_WELCOME_PATH, '/account?welcome=1')
  assert.equal(shouldShowRegistrationWelcome('1'), true)
  assert.equal(shouldShowRegistrationWelcome('0'), false)
  assert.equal(shouldShowRegistrationWelcome(null), false)
})

test('authenticated users leave the login page through a safe permitted internal route', () => {
  const session = {
    authenticated: true,
    can_access_admin: true,
    permissions: ['admin:access', 'users:manage', 'file:transfer'],
  }
  assert.equal(authenticatedEntryRedirect(session, null), '/account')
  assert.equal(authenticatedEntryRedirect(session, '/transfer?tab=recent#upload'), '/transfer?tab=recent#upload')
  assert.equal(authenticatedEntryRedirect(session, 'https://example.com'), '/account')
  assert.equal(authenticatedEntryRedirect(session, '//example.com'), '/account')
  assert.equal(authenticatedEntryRedirect(session, '/\\example.com'), '/account')
  assert.equal(authenticatedEntryRedirect(session, '/%5C%5Cexample.com'), '/account')
  assert.equal(authenticatedEntryRedirect(session, '/%2F%2Fexample.com'), '/account')
  assert.equal(authenticatedEntryRedirect(session, '/%252F%252Fexample.com'), '/account')
  assert.equal(authenticatedEntryRedirect(session, '/%ZZ'), '/account')
  assert.equal(authenticatedEntryRedirect(session, '/login?mode=register'), '/account')
  assert.equal(authenticatedEntryRedirect({
    authenticated: true,
    can_access_admin: false,
  }, '/admin/users'), '/account')
  assert.equal(authenticatedEntryRedirect({
    authenticated: true,
    can_access_admin: true,
    permissions: ['admin:access'],
  }, '/admin/users'), '/account')
  assert.equal(authenticatedEntryRedirect({
    authenticated: true,
    anonymous_transfer_upload_enabled: true,
    permissions: [],
  }, '/transfer'), '/account')
  assert.equal(authenticatedEntryRedirect({
    authenticated: true,
    must_change_password: true,
  }, '/transfer'), '/account?password_change=required')
})
