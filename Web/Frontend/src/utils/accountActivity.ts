import type { AccountActivitySource } from '../types/site'

export function accountActivitySourceLabel(source: AccountActivitySource): string {
  if (source === 'administrator') return '管理员操作'
  if (source === 'anonymous') return '未登录请求'
  return '本人操作'
}
