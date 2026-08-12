import assert from 'node:assert/strict'
import test from 'node:test'

import { getTransferAccessState, getTransferLoginUrl } from '../src/utils/transferAccess.ts'

test('the transfer panel waits for the session before choosing a route', () => {
  assert.equal(getTransferAccessState(null), 'loading')
})

test('anonymous sessions select the login route before protected content', () => {
  assert.equal(getTransferAccessState({ authenticated: false }), 'login')
  assert.equal(getTransferLoginUrl('/transfer'), '/login?next=%2Ftransfer')
})

test('authenticated sessions may mount the protected panel', () => {
  assert.equal(getTransferAccessState({ authenticated: true, username: 'operator' }), 'ready')
})

test('the login redirect preserves the complete internal destination', () => {
  assert.equal(
    getTransferLoginUrl('/transfer', '?tab=recent', '#upload'),
    '/login?next=%2Ftransfer%3Ftab%3Drecent%23upload',
  )
})
