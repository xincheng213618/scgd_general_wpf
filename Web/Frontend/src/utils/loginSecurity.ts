import type {
  LoginSecurityAccountType,
  LoginSecurityListParams,
  RegistrationSecurityClearResult,
  RegistrationSecurityListParams,
} from '../types/admin'

export function buildLoginSecuritySearchParams(params: LoginSecurityListParams = {}) {
  const pageSize = Math.max(1, params.pageSize ?? 20)
  const current = Math.max(1, params.current ?? 1)
  const search = new URLSearchParams({
    limit: String(pageSize),
    offset: String((current - 1) * pageSize),
  })
  const query = params.query?.trim()
  if (query) search.set('q', query)
  if (params.status) search.set('status', params.status)
  return search
}

export function loginSecurityAccountTypeLabel(type: LoginSecurityAccountType): string {
  return ({
    registered: '注册账号',
    config_admin: '配置管理员',
    unknown: '未知用户名',
  } as Record<LoginSecurityAccountType, string>)[type]
}

export function buildRegistrationSecuritySearchParams(
  params: RegistrationSecurityListParams = {},
) {
  const pageSize = Math.max(1, params.pageSize ?? 20)
  const current = Math.max(1, params.current ?? 1)
  const search = new URLSearchParams({
    limit: String(pageSize),
    offset: String((current - 1) * pageSize),
  })
  const query = params.query?.trim()
  if (query) search.set('q', query)
  if (params.status) search.set('status', params.status)
  return search
}

export function registrationLimitReasonLabel(reason: string): string {
  return ({
    attempt_velocity: '尝试次数超限',
    success_velocity: '成功注册数超限',
    'attempt_velocity+success_velocity': '尝试与成功注册均超限',
  } as Record<string, string>)[reason] ?? '注册计数中'
}

export function registrationClearSuccessMessage(
  result: Pick<RegistrationSecurityClearResult, 'cleared' | 'pending_count'>,
): string {
  if (!result.cleared) return '该来源的注册计数已自动到期'
  if (result.pending_count > 0) {
    return `已清除现有计数；${result.pending_count} 个处理中请求完成后会重新计数`
  }
  return '该来源的注册限制和计数已清除'
}
