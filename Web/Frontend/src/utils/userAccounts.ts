import type {
  UserAccount,
  UserAccountOrigin,
  UserBulkSecurityResult,
  UserDeleteResult,
  UserListParams,
  UserPasswordState,
  UserRecoveryState,
  UserRole,
  UserSortField,
  UserSortOrder,
  UserStatusUpdateResult,
} from '../types/admin'

export const MIN_ACCOUNT_PASSWORD_LENGTH = 15
export const MAX_ACCOUNT_PASSWORD_LENGTH = 128
export const ACCOUNT_PASSWORD_HELP = '至少 15 个字符，支持空格、中文和密码短语，不要求特定字符组合'
export const ACCOUNT_PASSWORD_CHANGE_HELP = `${ACCOUNT_PASSWORD_HELP}；必须与当前密码不同`

export function accountPasswordLength(password: string): number {
  return Array.from(password).length
}

export function accountPasswordValidationMessage(password: string): string | null {
  const length = accountPasswordLength(password)
  if (length < MIN_ACCOUNT_PASSWORD_LENGTH) {
    return `密码至少需要 ${MIN_ACCOUNT_PASSWORD_LENGTH} 个字符`
  }
  if (length > MAX_ACCOUNT_PASSWORD_LENGTH) {
    return `密码不能超过 ${MAX_ACCOUNT_PASSWORD_LENGTH} 个字符`
  }
  return null
}

export function accountPasswordChangeValidationMessage(
  currentPassword: string,
  newPassword: string,
): string | null {
  return accountPasswordValidationMessage(newPassword)
    ?? (currentPassword === newPassword ? '新密码不能与当前密码相同' : null)
}

export const USER_ROLE_OPTIONS: Array<{
  label: string
  value: UserRole
  description: string
}> = [
  {
    label: '普通用户',
    value: 'user',
    description: '当前默认拥有与管理员相同的功能权限，可在权限管理中调整',
  },
  {
    label: '管理员',
    value: 'admin',
    description: '可进入管理后台并执行全部管理操作',
  },
]

export function userRoleLabel(role: UserRole): string {
  return role === 'admin' ? '管理员' : '普通用户'
}

export function oppositeUserRole(role: UserRole): UserRole {
  return role === 'admin' ? 'user' : 'admin'
}

export function passwordResetSuccessMessage(
  currentSessionPreserved: boolean,
  recoveryRequestsResolved = 0,
  loginFailureSourcesCleared = 0,
): string {
  const recovery = recoveryRequestsResolved > 0 ? '；找回申请已处理' : ''
  const loginFailures = loginFailureSourcesCleared > 0
    ? `；已清除 ${loginFailureSourcesCleared} 个登录失败来源`
    : ''
  const message = currentSessionPreserved
    ? '密码已更新；其他登录会话已失效'
    : '密码已重置；该账号现有会话已失效，下次登录须先修改临时密码'
  return `${message}${recovery}${loginFailures}`
}

export function userAccountOriginLabel(origin: UserAccountOrigin): string {
  if (origin === 'self_registered') return '公开注册'
  if (origin === 'administrator_created') return '管理员创建'
  return '历史账号'
}

export function passwordChangeRequiredSuccessMessage(
  revoked: number,
  loginFailureSourcesCleared = 0,
): string {
  const message = revoked > 0
    ? `已要求下次登录改密；已注销 ${revoked} 个有效会话`
    : '已要求下次登录改密；该账号当前没有有效会话'
  return loginFailureSourcesCleared > 0
    ? `${message}；已清除 ${loginFailureSourcesCleared} 个登录失败来源`
    : message
}

export function accountStatusSuccessMessage(result: UserStatusUpdateResult): string {
  const details: string[] = []
  if (result.sessions_revoked > 0) details.push(`已注销 ${result.sessions_revoked} 个有效会话`)
  if (result.password_recovery_requests_resolved > 0) details.push('找回申请已处理')
  if (result.login_failure_sources_cleared > 0) {
    details.push(`已清除 ${result.login_failure_sources_cleared} 个登录失败来源`)
  }
  const status = result.is_active ? '账号已启用' : '账号已停用'
  return details.length > 0 ? `${status}；${details.join('；')}` : `${status}；安全状态已同步`
}

export function forceLogoutSuccessMessage(revoked: number): string {
  return revoked > 0
    ? `已注销 ${revoked} 个有效会话`
    : '该账号当前没有有效会话'
}

export function bulkSecurityActionResultMessage(result: UserBulkSecurityResult): string {
  const action = result.action === 'force_logout' ? '批量强制下线' : '批量要求改密'
  const failure = result.failed > 0 ? `，失败 ${result.failed} 个` : ''
  const cleared = result.login_failure_sources_cleared > 0
    ? `，清除 ${result.login_failure_sources_cleared} 个登录失败来源`
    : ''
  return `${action}完成：成功 ${result.succeeded} 个${failure}，注销 ${result.sessions_revoked} 个有效会话${cleared}`
}

const NON_RETRYABLE_BULK_SECURITY_CODES = new Set([
  'user_not_found',
  'config_admin_managed',
  'current_session_account',
])

export function retryableBulkSecurityUserIds(result: UserBulkSecurityResult): number[] {
  return [...new Set(result.results
    .filter((item) => (
      item.status === 'failed'
      && !NON_RETRYABLE_BULK_SECURITY_CODES.has(item.code || '')
    ))
    .map((item) => item.user_id))]
}

export function canManageUserAccount(
  account: Pick<UserAccount, 'is_config_admin'>,
): boolean {
  return account.is_config_admin !== true
}

export function canDeleteUserAccount(
  account: Pick<UserAccount, 'is_active' | 'is_config_admin' | 'is_current'>,
): boolean {
  return canManageUserAccount(account) && !account.is_active && account.is_current !== true
}

export function userDeletionSuccessMessage(result: UserDeleteResult): string {
  const details: string[] = []
  if (result.sessions_deleted > 0) details.push(`清理 ${result.sessions_deleted} 条会话`)
  if (result.password_recovery_requests_deleted > 0) {
    details.push(`清理 ${result.password_recovery_requests_deleted} 条找回记录`)
  }
  if (result.login_failure_sources_cleared > 0) {
    details.push(`清理 ${result.login_failure_sources_cleared} 个登录失败来源`)
  }
  const suffix = details.length > 0 ? `；${details.join('；')}` : ''
  return `账号 ${result.username} 已永久删除${suffix}`
}

const USER_SORT_FIELDS = new Set<UserSortField>([
  'username',
  'display_name',
  'email',
  'role',
  'account_origin',
  'is_active',
  'active_session_count',
  'created_at',
  'last_login_at',
  'password_recovery_requested_at',
])

export function resolveUserListSort(
  tableSort: Record<string, unknown>,
): { sortBy?: UserSortField, sortOrder?: UserSortOrder } {
  for (const [field, direction] of Object.entries(tableSort)) {
    if (!USER_SORT_FIELDS.has(field as UserSortField)) continue
    if (direction === 'ascend') {
      return { sortBy: field as UserSortField, sortOrder: 'asc' }
    }
    if (direction === 'descend') {
      return { sortBy: field as UserSortField, sortOrder: 'desc' }
    }
  }
  return {}
}

export function buildUserListSearchParams(params: UserListParams): URLSearchParams {
  const pageSize = params.pageSize ?? 20
  const current = params.current ?? 1
  const search = new URLSearchParams({
    limit: String(pageSize),
    offset: String(Math.max(0, current - 1) * pageSize),
  })
  const query = params.query?.trim()
  if (query) search.set('q', query)
  if (params.role) search.set('role', params.role)
  if (params.origin) search.set('origin', params.origin)
  if (params.status) search.set('status', params.status)
  if (params.passwordState) search.set('password_state', params.passwordState)
  if (params.recoveryState) search.set('recovery_state', params.recoveryState)
  if (params.sortBy) {
    search.set('sort_by', params.sortBy)
    search.set('sort_order', params.sortOrder ?? 'desc')
  }
  return search
}

export function resolveUserListEntryFilters(search: URLSearchParams): {
  password_state?: UserPasswordState
  recovery_state?: UserRecoveryState
} {
  const passwordState = search.get('password_state')
  const recoveryState = search.get('recovery_state')
  return {
    ...(passwordState === 'pending' || passwordState === 'ready'
      ? { password_state: passwordState }
      : {}),
    ...(recoveryState === 'pending' || recoveryState === 'none'
      ? { recovery_state: recoveryState }
      : {}),
  }
}

export function buildUserDetailsSearchParams(
  params: { current?: number, pageSize?: number } = {},
): URLSearchParams {
  const pageSize = params.pageSize ?? 8
  const current = params.current ?? 1
  return new URLSearchParams({
    activity_limit: String(pageSize),
    activity_offset: String(Math.max(0, current - 1) * pageSize),
  })
}
