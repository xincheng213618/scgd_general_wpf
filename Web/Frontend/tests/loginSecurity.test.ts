import assert from 'node:assert/strict'
import test from 'node:test'
import {
  buildLoginSecuritySearchParams,
  buildRegistrationSecuritySearchParams,
  loginSecurityAccountTypeLabel,
  registrationClearSuccessMessage,
  registrationLimitReasonLabel,
} from '../src/utils/loginSecurity.ts'

test('login security queries normalize paging and optional filters', () => {
  const search = buildLoginSecuritySearchParams({
    current: 3,
    pageSize: 10,
    query: '  worker@example.com  ',
    status: 'locked',
  })
  assert.equal(search.get('limit'), '10')
  assert.equal(search.get('offset'), '20')
  assert.equal(search.get('q'), 'worker@example.com')
  assert.equal(search.get('status'), 'locked')
})

test('login security account types remain explicit', () => {
  assert.equal(loginSecurityAccountTypeLabel('registered'), '注册账号')
  assert.equal(loginSecurityAccountTypeLabel('config_admin'), '配置管理员')
  assert.equal(loginSecurityAccountTypeLabel('unknown'), '未知用户名')
})

test('registration security queries normalize source filters and paging', () => {
  const search = buildRegistrationSecuritySearchParams({
    current: 2,
    pageSize: 25,
    query: '  198.51.100  ',
    status: 'blocked',
  })
  assert.equal(search.get('limit'), '25')
  assert.equal(search.get('offset'), '25')
  assert.equal(search.get('q'), '198.51.100')
  assert.equal(search.get('status'), 'blocked')
})

test('registration security actions explain limits and in-flight requests', () => {
  assert.equal(registrationLimitReasonLabel('attempt_velocity'), '尝试次数超限')
  assert.equal(registrationLimitReasonLabel('success_velocity'), '成功注册数超限')
  assert.match(registrationClearSuccessMessage({ cleared: true, pending_count: 2 }), /2 个处理中/)
  assert.match(registrationClearSuccessMessage({ cleared: true, pending_count: 0 }), /已清除/)
  assert.match(registrationClearSuccessMessage({ cleared: false, pending_count: 0 }), /自动到期/)
})
