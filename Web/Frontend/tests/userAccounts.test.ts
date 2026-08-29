import assert from 'node:assert/strict'
import test from 'node:test'
import {
  MAX_ACCOUNT_PASSWORD_LENGTH,
  MIN_ACCOUNT_PASSWORD_LENGTH,
  ACCOUNT_PASSWORD_CHANGE_HELP,
  accountPasswordLength,
  accountPasswordChangeValidationMessage,
  accountPasswordValidationMessage,
  accountStatusSuccessMessage,
  buildUserListSearchParams,
  buildUserDetailsSearchParams,
  bulkSecurityActionResultMessage,
  canDeleteUserAccount,
  canManageUserAccount,
  forceLogoutSuccessMessage,
  oppositeUserRole,
  passwordChangeRequiredSuccessMessage,
  passwordResetSuccessMessage,
  resolveUserListEntryFilters,
  resolveUserListSort,
  retryableBulkSecurityUserIds,
  USER_ROLE_OPTIONS,
  userAccountOriginLabel,
  userDeletionSuccessMessage,
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
  assert.equal(MIN_ACCOUNT_PASSWORD_LENGTH, 15)
  assert.equal(MAX_ACCOUNT_PASSWORD_LENGTH, 128)
  assert.equal(accountPasswordLength('🔐'.repeat(15)), 15)
  assert.match(accountPasswordValidationMessage('a'.repeat(14)) || '', /15/)
  assert.equal(accountPasswordValidationMessage('a'.repeat(15)), null)
  assert.equal(accountPasswordValidationMessage('🔐'.repeat(15)), null)
  assert.match(accountPasswordValidationMessage('a'.repeat(129)) || '', /128/)
  assert.match(ACCOUNT_PASSWORD_CHANGE_HELP, /必须与当前密码不同/)
  assert.match(
    accountPasswordChangeValidationMessage('correct-horse-1', 'correct-horse-1') || '',
    /不能与当前密码相同/,
  )
  assert.equal(
    accountPasswordChangeValidationMessage('correct-horse-1', 'correct-horse-2'),
    null,
  )
  assert.match(passwordResetSuccessMessage(true), /其他登录会话已失效/)
  assert.match(passwordResetSuccessMessage(false), /现有会话已失效/)
  assert.match(passwordResetSuccessMessage(false), /下次登录须先修改临时密码/)
  assert.match(passwordResetSuccessMessage(false, 1, 2), /找回申请已处理/)
  assert.match(passwordResetSuccessMessage(false, 1, 2), /清除 2 个登录失败来源/)
  assert.match(passwordChangeRequiredSuccessMessage(2, 1), /已注销 2 个有效会话/)
  assert.match(passwordChangeRequiredSuccessMessage(2, 1), /清除 1 个登录失败来源/)
  assert.match(passwordChangeRequiredSuccessMessage(0), /没有有效会话/)
  assert.equal(forceLogoutSuccessMessage(3), '已注销 3 个有效会话')
  assert.equal(forceLogoutSuccessMessage(0), '该账号当前没有有效会话')
})

test('account status feedback reports every security state that was settled', () => {
  assert.equal(accountStatusSuccessMessage({
    id: 8,
    username: 'worker',
    display_name: '',
    email: '',
    role: 'user',
    account_origin: 'self_registered',
    is_active: false,
    must_change_password: false,
    active_session_count: 0,
    sessions_revoked: 2,
    password_recovery_requests_resolved: 1,
    login_failure_sources_cleared: 3,
  }), '账号已停用；已注销 2 个有效会话；找回申请已处理；已清除 3 个登录失败来源')
  assert.match(accountStatusSuccessMessage({
    id: 8,
    username: 'worker',
    display_name: '',
    email: '',
    role: 'user',
    account_origin: 'self_registered',
    is_active: true,
    must_change_password: false,
    active_session_count: 0,
    sessions_revoked: 0,
    password_recovery_requests_resolved: 0,
    login_failure_sources_cleared: 0,
  }), /安全状态已同步/)
})

test('account origins stay readable and distinguish durable creation paths', () => {
  assert.equal(userAccountOriginLabel('self_registered'), '公开注册')
  assert.equal(userAccountOriginLabel('administrator_created'), '管理员创建')
  assert.equal(userAccountOriginLabel('legacy'), '历史账号')
})

test('the configured administrator stays outside database user management', () => {
  assert.equal(canManageUserAccount({ is_config_admin: true }), false)
  assert.equal(canManageUserAccount({ is_config_admin: false }), true)
  assert.equal(canManageUserAccount({}), true)
})

test('permanent deletion is available only after an account is disabled', () => {
  assert.equal(canDeleteUserAccount({ is_active: false, is_current: false }), true)
  assert.equal(canDeleteUserAccount({ is_active: true, is_current: false }), false)
  assert.equal(canDeleteUserAccount({ is_active: false, is_current: true }), false)
  assert.equal(canDeleteUserAccount({
    is_active: false,
    is_current: false,
    is_config_admin: true,
  }), false)
  assert.equal(userDeletionSuccessMessage({
    status: 'deleted',
    id: 9,
    username: 'retired-user',
    role: 'user',
    account_origin: 'self_registered',
    sessions_deleted: 2,
    password_recovery_requests_deleted: 1,
    login_failure_sources_cleared: 3,
  }), '账号 retired-user 已永久删除；清理 2 条会话；清理 1 条找回记录；清理 3 个登录失败来源')
})

test('user list query normalizes paging and optional filters', () => {
  const search = buildUserListSearchParams({
    current: 3,
    pageSize: 10,
    query: '  alpha@example.com  ',
    role: 'user',
    origin: 'self_registered',
    status: 'inactive',
    passwordState: 'pending',
    recoveryState: 'pending',
    sortBy: 'last_login_at',
    sortOrder: 'asc',
  })

  assert.equal(search.get('limit'), '10')
  assert.equal(search.get('offset'), '20')
  assert.equal(search.get('q'), 'alpha@example.com')
  assert.equal(search.get('role'), 'user')
  assert.equal(search.get('origin'), 'self_registered')
  assert.equal(search.get('status'), 'inactive')
  assert.equal(search.get('password_state'), 'pending')
  assert.equal(search.get('recovery_state'), 'pending')
  assert.equal(search.get('sort_by'), 'last_login_at')
  assert.equal(search.get('sort_order'), 'asc')
})

test('user list query protects the first page offset', () => {
  const search = buildUserListSearchParams({ current: 0, pageSize: 20 })
  assert.equal(search.get('offset'), '0')
  assert.equal(search.has('q'), false)
})

test('user management entry links apply only supported security filters', () => {
  assert.deepEqual(resolveUserListEntryFilters(new URLSearchParams(
    'recovery_state=pending&password_state=ready',
  )), {
    recovery_state: 'pending',
    password_state: 'ready',
  })
  assert.deepEqual(resolveUserListEntryFilters(new URLSearchParams(
    'recovery_state=expired&password_state=unsafe',
  )), {})
})

test('account details query pages only the scoped activity timeline', () => {
  const search = buildUserDetailsSearchParams({ current: 3, pageSize: 8 })
  assert.equal(search.get('activity_limit'), '8')
  assert.equal(search.get('activity_offset'), '16')

  const firstPage = buildUserDetailsSearchParams({ current: 0 })
  assert.equal(firstPage.get('activity_limit'), '8')
  assert.equal(firstPage.get('activity_offset'), '0')
})

test('user table sorting is reduced to the backend allowlist', () => {
  assert.deepEqual(resolveUserListSort({ created_at: 'descend' }), {
    sortBy: 'created_at',
    sortOrder: 'desc',
  })
  assert.deepEqual(resolveUserListSort({ username: 'ascend' }), {
    sortBy: 'username',
    sortOrder: 'asc',
  })
  assert.deepEqual(resolveUserListSort({ account_origin: 'ascend' }), {
    sortBy: 'account_origin',
    sortOrder: 'asc',
  })
  assert.deepEqual(resolveUserListSort({ password_recovery_requested_at: 'descend' }), {
    sortBy: 'password_recovery_requested_at',
    sortOrder: 'desc',
  })
  assert.deepEqual(resolveUserListSort({ password_hash: 'ascend' }), {})
  assert.deepEqual(resolveUserListSort({ role: null }), {})
})

test('bulk user security results report partial outcomes and revoked sessions', () => {
  assert.equal(bulkSecurityActionResultMessage({
    action: 'force_logout',
    requested: 3,
    succeeded: 2,
    failed: 1,
    sessions_revoked: 4,
    login_failure_sources_cleared: 0,
    results: [],
  }), '批量强制下线完成：成功 2 个，失败 1 个，注销 4 个有效会话')
  assert.equal(bulkSecurityActionResultMessage({
    action: 'require_password_change',
    requested: 2,
    succeeded: 2,
    failed: 0,
    sessions_revoked: 1,
    login_failure_sources_cleared: 3,
    results: [],
  }), '批量要求改密完成：成功 2 个，注销 1 个有效会话，清除 3 个登录失败来源')
})

test('bulk security retries retain only actionable failed accounts', () => {
  assert.deepEqual(retryableBulkSecurityUserIds({
    action: 'force_logout',
    requested: 7,
    succeeded: 1,
    failed: 6,
    sessions_revoked: 1,
    login_failure_sources_cleared: 0,
    results: [
      { user_id: 1, username: 'done', status: 'succeeded' },
      { user_id: 2, username: 'retry', status: 'failed', code: 'operation_failed' },
      { user_id: 2, username: 'retry', status: 'failed', code: 'operation_failed' },
      { user_id: 3, username: '', status: 'failed', code: 'user_not_found' },
      { user_id: 4, username: 'admin', status: 'failed', code: 'config_admin_managed' },
      { user_id: 5, username: 'current', status: 'failed', code: 'current_session_account' },
      { user_id: 6, username: 'service-error', status: 'failed', code: 'password_service_unavailable' },
    ],
  }), [2, 6])
})
