import type {
  AdminStats,
  ApiKeyItem,
  AuditLogResponse,
  CacheStatus,
  CopilotProfile,
  CopilotProfilePayload,
  CreateApiKeyPayload,
  CreateApiKeyResult,
  DocsStatus,
  DeploymentHistoryResponse,
  PublishIntegrityReport,
  ScheduledJob,
  TrafficStatsResponse,
  UserAccount,
} from '../types/admin'
import { deleteJson, getJson, postJson, putJson } from './request'

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
  since?: string
  until?: string
}) {
  const pageSize = params.pageSize ?? 20
  const current = params.current ?? 1
  const search = new URLSearchParams()
  search.set('limit', String(pageSize))
  search.set('offset', String((current - 1) * pageSize))
  if (params.action) search.set('action', params.action)
  if (params.actor) search.set('actor', params.actor)
  if (params.target) search.set('target', params.target)
  if (params.since) search.set('since', params.since)
  if (params.until) search.set('until', params.until)
  return getJson<AuditLogResponse>(`/api/admin/audit-log?${search.toString()}`)
}

export function getDeploymentHistory(params: {
  current?: number
  pageSize?: number
  status?: string
  source?: string
  commit?: string
}) {
  const pageSize = params.pageSize ?? 20
  const current = params.current ?? 1
  const search = new URLSearchParams()
  search.set('limit', String(pageSize))
  search.set('offset', String((current - 1) * pageSize))
  if (params.status) search.set('status', params.status)
  if (params.source) search.set('source', params.source)
  if (params.commit) search.set('commit', params.commit)
  return getJson<DeploymentHistoryResponse>(`/api/admin/deployments?${search.toString()}`)
}

export function listUsers() {
  return getJson<UserAccount[]>('/api/admin/users')
}

export function setUserEnabled(id: number, enabled: boolean) {
  return postJson<UserAccount>(`/api/admin/users/${id}/${enabled ? 'enable' : 'disable'}`)
}

export function listCopilotProfiles() {
  return getJson<CopilotProfile[]>('/api/admin/copilot/profiles')
}

export function createCopilotProfile(payload: CopilotProfilePayload) {
  return postJson<CopilotProfile>('/api/admin/copilot/profiles', payload)
}

export function updateCopilotProfile(id: string, payload: CopilotProfilePayload) {
  return putJson<CopilotProfile>(`/api/admin/copilot/profiles/${encodeURIComponent(id)}`, payload)
}

export function deleteCopilotProfile(id: string) {
  return deleteJson<{ status: string; id: string }>(
    `/api/admin/copilot/profiles/${encodeURIComponent(id)}`,
  )
}
