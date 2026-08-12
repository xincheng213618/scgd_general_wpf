import assert from 'node:assert/strict'
import test from 'node:test'
import {
  MIN_ACCOUNT_PASSWORD_LENGTH,
  oppositeUserRole,
  passwordResetSuccessMessage,
  USER_ROLE_OPTIONS,
  userRoleLabel,
} from '../src/utils/userAccounts.ts'

test('account role controls expose only supported backend roles', () => {
  assert.deepEqual(USER_ROLE_OPTIONS.map((option) => option.value), ['user', 'admin'])
  assert.equal(userRoleLabel('admin'), '管理员')
  assert.equal(userRoleLabel('user'), '普通用户')
  assert.equal(oppositeUserRole('admin'), 'user')
  assert.equal(oppositeUserRole('user'), 'admin')
})

test('password guidance matches the backend contract and session outcome', () => {
  assert.equal(MIN_ACCOUNT_PASSWORD_LENGTH, 6)
  assert.match(passwordResetSuccessMessage(true), /其他登录会话已失效/)
  assert.match(passwordResetSuccessMessage(false), /现有会话已失效/)
})
