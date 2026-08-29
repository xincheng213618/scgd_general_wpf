import assert from 'node:assert/strict'
import test from 'node:test'

import { getTransferAccessState, getTransferLoginUrl } from '../src/utils/transferAccess.ts'

test('the transfer panel waits for the session before choosing a route', () => {
  assert.equal(getTransferAccessState(null), 'loading')
})

test('anonymous sessions select the login route when guest upload is disabled', () => {
  assert.equal(getTransferAccessState({ authenticated: false }), 'login')
  assert.equal(getTransferLoginUrl('/transfer'), '/login?next=%2Ftransfer')
})

test('anonymous sessions may mount the upload-only transfer panel when enabled', () => {
  assert.equal(getTransferAccessState({
    authenticated: false,
    anonymous_transfer_upload_enabled: true,
  }), 'ready')
})

test('authenticated sessions may mount the protected panel', () => {
  assert.equal(getTransferAccessState({
    authenticated: true,
    username: 'operator',
    permissions: ['file:transfer'],
  }), 'ready')
})

test('authenticated sessions cannot fall back to guest transfer after permission removal', () => {
  assert.equal(getTransferAccessState({
    authenticated: true,
    username: 'operator',
    permissions: [],
    anonymous_transfer_upload_enabled: true,
  }), 'forbidden')
})

test('temporary-password sessions must finish password change before transfer access', () => {
  assert.equal(getTransferAccessState({
    authenticated: true,
    username: 'operator',
    must_change_password: true,
    anonymous_transfer_upload_enabled: true,
  }), 'password-change')
})

test('the login redirect preserves the complete internal destination', () => {
  assert.equal(
    getTransferLoginUrl('/transfer', '?tab=recent', '#upload'),
    '/login?next=%2Ftransfer%3Ftab%3Drecent%23upload',
  )
})
