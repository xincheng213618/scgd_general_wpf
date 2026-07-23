import type {
  AdminStats,
  ApiKeyItem,
  AuditLogResponse,
  CacheStatus,
  CreateApiKeyPayload,
  CreateApiKeyResult,
  DocsStatus,
  PublishIntegrityReport,
  ScheduledJob,
  TrafficStatsResponse,
} from '../types/admin'
import { getJson, postJson } from './request'

export function getAdminStats() {
  return getJson<AdminStats>('/api/admin/stats/overview')
}

export function getTrafficStats(days: number, limit = 10, signal?: AbortSignal) {
  const search = new URLSearchParams({ days: String(days), limit: String(limit) })
  return getJson<TrafficStatsResponse>(`/api/admin/stats/traffic?${search.toString()}`, signal)
}

export function getCacheStatus() {
  return getJson<CacheStatus>('/api/admin/cache/status')
}

export function getDocsStatus() {
  return getJson<DocsStatus>('/api/admin/docs/status')
}

export function getPublishIntegrity() {
  return getJson<PublishIntegrityReport>('/api/admin/publish/integrity')
}

export function refreshAllIndexes() {
  return postJson('/api/admin/index/refresh-all')
}

export function refreshDocsIndex() {
  return postJson<{ status: string; indexed_count: number; duration_ms: number; errors?: string[] }>('/api/admin/index/docs/refresh')
}

export function cleanupCache() {
  return postJson<{ deleted_count: number }>('/api/admin/cache/cleanup')
}

export function listJobs() {
  return getJson<ScheduledJob[]>('/api/admin/jobs')
}

export function runJob(jobId: string) {
  return postJson(`/api/admin/jobs/${encodeURIComponent(jobId)}/run`)
}

export function setJobEnabled(jobId: string, enabled: boolean) {
  return postJson(`/api/admin/jobs/${encodeURIComponent(jobId)}/${enabled ? 'enable' : 'disable'}`)
}

export function listApiKeys() {
  return getJson<ApiKeyItem[]>('/api/admin/api-keys')
}

export function createApiKey(payload: CreateApiKeyPayload) {
  return postJson<CreateApiKeyResult>('/api/admin/api-keys', payload)
}

export function revokeApiKey(id: number) {
  return postJson(`/api/admin/api-keys/${id}/revoke`)
}

export function rotateApiKey(id: number) {
  return postJson<CreateApiKeyResult>(`/api/admin/api-keys/${id}/rotate`)
}

export function getAuditLog(params: {
  current?: number
  pageSize?: number
  action?: string
  actor?: string
  target?: string
}) {
  const pageSize = params.pageSize ?? 20
  const current = params.current ?? 1
  const search = new URLSearchParams()
  search.set('limit', String(pageSize))
  search.set('offset', String((current - 1) * pageSize))
  if (params.action) search.set('action', params.action)
  if (params.actor) search.set('actor', params.actor)
  if (params.target) search.set('target', params.target)
  return getJson<AuditLogResponse>(`/api/admin/audit-log?${search.toString()}`)
}
