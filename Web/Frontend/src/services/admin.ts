import type {
  AccountSettingsResponse,
  AccountSettingsUpdateResponse,
  AccountSettingsValues,
  AdminStats,
  AllIndexRefreshResult,
  ApiKeyItem,
  ApiKeyScopeCatalog,
  ApiKeyUsage,
  AuditLogResponse,
  CacheStatus,
  DatabaseBackupInventory,
  DatabaseBackupResult,
  CopilotProfile,
  CopilotProfilePayload,
  CreateUserPayload,
  CreateApiKeyPayload,
  CreateApiKeyResult,
  DocsStatus,
  DeploymentHistoryResponse,
  FeedbackDetail,
  FeedbackInboxFilter,
  FeedbackInboxResponse,
  FeedbackStatus,
  PublishIntegrityReport,
  PerformanceSummary,
  IndexRefreshResult,
  IndexScope,
  IndexStatusResponse,
  JobRunPage,
  JobRunResult,
  OperationsOverview,
  ScheduledJob,
  RetentionSettingsResponse,
  RetentionSettingsUpdateResponse,
  RetentionSettingsValues,
  TrafficStatsResponse,
  UserAccount,
  UserPasswordResetResult,
  UserRole,
} from '../types/admin'
import { deleteJson, getJson, postJson, putJson } from './request'

export function getAdminStats() {
  return getJson<AdminStats>('/api/admin/stats/overview')
}

export function getTrafficStats(days: number, limit = 10, signal?: AbortSignal) {
  const search = new URLSearchParams({ days: String(days), limit: String(limit) })
  return getJson<TrafficStatsResponse>(`/api/admin/stats/traffic?${search.toString()}`, signal)
}

export function getPerformanceSummary(signal?: AbortSignal) {
  return getJson<PerformanceSummary>('/api/admin/perf/summary', signal)
}

export function getOperationsOverview(signal?: AbortSignal) {
  return getJson<OperationsOverview>(
    '/api/admin/operations/overview?hostLimit=100&activityLimit=100',
    signal,
  )
}

export function getCacheStatus() {
  return getJson<CacheStatus>('/api/admin/cache/status')
}

export function getIndexStatus() {
  return getJson<IndexStatusResponse>('/api/admin/index/status')
}

export function getDocsStatus() {
  return getJson<DocsStatus>('/api/admin/docs/status')
}

export function getPublishIntegrity() {
  return getJson<PublishIntegrityReport>('/api/admin/publish/integrity')
}

export function refreshAllIndexes() {
  return postJson<AllIndexRefreshResult>('/api/admin/index/refresh-all')
}

const indexRefreshPaths: Record<IndexScope, string> = {
  plugins: '/api/admin/index/plugins/refresh',
  releases: '/api/admin/index/releases/refresh',
  updates: '/api/admin/index/updates/refresh',
  tools: '/api/admin/index/tools/refresh',
  docs: '/api/admin/index/docs/refresh',
}

export function refreshIndex(scope: IndexScope) {
  return postJson<IndexRefreshResult>(indexRefreshPaths[scope])
}

export function refreshDocsIndex() {
  return postJson<{ status: string; indexed_count: number; duration_ms: number; errors?: string[] }>('/api/admin/index/docs/refresh')
}

export function cleanupCache() {
  return postJson<{ deleted_count: number }>('/api/admin/cache/cleanup')
}

export function listDatabaseBackups() {
  return getJson<DatabaseBackupInventory>('/api/admin/backup/db')
}

export function backupDatabase() {
  return postJson<DatabaseBackupResult>('/api/admin/backup/db')
}

export function getRetentionSettings(signal?: AbortSignal) {
  return getJson<RetentionSettingsResponse>('/api/admin/settings/retention', signal)
}

export function updateRetentionSettings(values: RetentionSettingsValues) {
  return putJson<RetentionSettingsUpdateResponse>('/api/admin/settings/retention', { values })
}

export function getAccountSettings(signal?: AbortSignal) {
  return getJson<AccountSettingsResponse>('/api/admin/settings/accounts', signal)
}

export function updateAccountSettings(values: AccountSettingsValues) {
  return putJson<AccountSettingsUpdateResponse>('/api/admin/settings/accounts', values)
}

export function listJobs() {
  return getJson<ScheduledJob[]>('/api/admin/jobs')
}

export function runJob(jobId: string) {
  return postJson<JobRunResult>(`/api/admin/jobs/${encodeURIComponent(jobId)}/run`)
}

export function getJobRuns(jobId: string, params: {
  current?: number
  pageSize?: number
  status?: string
}) {
  const pageSize = params.pageSize ?? 20
  const current = params.current ?? 1
  const search = new URLSearchParams({
    limit: String(pageSize),
    offset: String((current - 1) * pageSize),
  })
  if (params.status && params.status !== 'all') search.set('status', params.status)
  return getJson<JobRunPage>(
    `/api/admin/jobs/${encodeURIComponent(jobId)}/runs?${search.toString()}`,
  )
}

export function setJobEnabled(jobId: string, enabled: boolean) {
  return postJson(`/api/admin/jobs/${encodeURIComponent(jobId)}/${enabled ? 'enable' : 'disable'}`)
}

export function listApiKeys() {
  return getJson<ApiKeyItem[]>('/api/admin/api-keys')
}

export function getApiKeyScopeCatalog(signal?: AbortSignal) {
  return getJson<ApiKeyScopeCatalog>('/api/admin/api-keys/scopes', signal)
}

export function getApiKeyUsage(id: number, signal?: AbortSignal) {
  return getJson<ApiKeyUsage>(`/api/admin/api-keys/${id}/usage`, signal)
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

export function getFeedbackInbox(params: {
  current?: number
  pageSize?: number
  status?: FeedbackInboxFilter
  query?: string
}) {
  const pageSize = params.pageSize ?? 20
  const current = params.current ?? 1
  const search = new URLSearchParams({
    limit: String(pageSize),
    offset: String((current - 1) * pageSize),
  })
  if (params.status) search.set('status', params.status)
  if (params.query) search.set('query', params.query)
  return getJson<FeedbackInboxResponse>(`/api/admin/feedback?${search.toString()}`)
}

export function getFeedbackDetail(feedbackId: string, signal?: AbortSignal) {
  return getJson<FeedbackDetail>(
    `/api/admin/feedback/${encodeURIComponent(feedbackId)}`,
    signal,
  )
}

export function updateFeedbackStatus(feedbackId: string, status: FeedbackStatus) {
  return putJson<FeedbackDetail>(
    `/api/admin/feedback/${encodeURIComponent(feedbackId)}/status`,
    { status },
  )
}

export function feedbackAttachmentUrl(feedbackId: string, filename: string) {
  return `/api/admin/feedback/${encodeURIComponent(feedbackId)}/attachments/${encodeURIComponent(filename)}`
}

export function createUserAccount(payload: CreateUserPayload) {
  return postJson<UserAccount>('/api/admin/users', payload)
}

export function setUserEnabled(id: number, enabled: boolean) {
  return postJson<UserAccount>(`/api/admin/users/${id}/${enabled ? 'enable' : 'disable'}`)
}

export function updateUserRole(id: number, role: UserRole) {
  return putJson<UserAccount>(`/api/admin/users/${id}/role`, { role })
}

export function resetUserPassword(id: number, password: string) {
  return postJson<UserPasswordResetResult>(`/api/admin/users/${id}/password`, { password })
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
