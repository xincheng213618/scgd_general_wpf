import assert from 'node:assert/strict'
import test from 'node:test'
import {
  publicAuthEntryLabel,
  resolveAuthEntryMode,
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
