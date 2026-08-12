import type { ApiKeyItem, ApiKeyStatus } from '../types/admin'

const apiKeyStatuses = new Set<ApiKeyStatus>([
  'active',
  'expired',
  'revoked',
  'invalid_expiry',
])

export function effectiveApiKeyStatus(
  key: ApiKeyItem,
  now: Date = new Date(),
): ApiKeyStatus {
  if (key.status && apiKeyStatuses.has(key.status)) return key.status
  if (!key.is_active || key.revoked_at) return 'revoked'
  if (!key.expires_at) return 'active'
  const expiry = Date.parse(key.expires_at)
  if (!Number.isFinite(expiry)) return 'invalid_expiry'
  return expiry <= now.getTime() ? 'expired' : 'active'
}

export function toUtcExpiry(value: unknown): string | undefined {
  if (value === undefined || value === null || value === '') return undefined
  if (typeof value === 'object' && 'toISOString' in value) {
    const iso = (value as { toISOString: () => string }).toISOString()
    if (iso) return iso
  }
  const parsed = new Date(String(value))
  if (Number.isNaN(parsed.getTime())) {
    throw new Error('过期时间格式无效')
  }
  return parsed.toISOString()
}
