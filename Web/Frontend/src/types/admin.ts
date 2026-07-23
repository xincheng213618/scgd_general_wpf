export type ThemeMode = 'system' | 'light' | 'dark'

export interface AdminStats {
  totalDownloads: number
  downloadsToday: number
  pluginCount: number
  packageCount: number
  latestReleaseVersion: string
  pluginCatalogCached: boolean
  dbSizeBytes: number
  visitsToday: number
  uniqueVisitorsToday: number
  avgResponseMsToday: number
  errorResponsesToday: number
}

export interface TrafficSummary {
  periodStart: string
  periodEnd: string
  days: number
  visits: number
  uniqueVisitorDays: number
  avgResponseMs: number
  errorResponses: number
  errorRate: number
  totalResponseBytes: number
}

export interface TrafficDayStats {
  day: string
  visits: number
  uniqueVisitors: number
  avgResponseMs: number
  maxResponseMs: number
  errorResponses: number
  errorRate: number
  totalDurationMs: number
  totalResponseBytes: number
}

export interface TrafficRouteStats {
  route: string
  method: string
  visits: number
  errorResponses: number
  errorRate: number
  avgResponseMs: number
  maxResponseMs: number
  responseBytes: number
}

export interface TrafficClientStats {
  client: 'desktop' | 'mobile' | 'tablet' | 'bot' | 'other'
  visits: number
  uniqueVisitorDays: number
  share: number
  errorResponses: number
  avgResponseMs: number
}

export interface TrafficRecorderStatus {
  pending: number
  dropped: number
  lastError: string | null
  lastFlushAt?: string | null
  capacity?: number
}

export interface TrafficStatsResponse {
  summary: TrafficSummary
  today: TrafficDayStats
  daily: TrafficDayStats[]
  topRoutes: TrafficRouteStats[]
  clients: TrafficClientStats[]
  recorder: TrafficRecorderStatus
}

export interface CacheStatus {
  cache_entry_count: number
  expired_cache_entry_count: number
  plugin_index_count: number
  package_index_count: number
  release_index_count: number
  update_index_count: number
  tool_index_count: number
  plugins_dir_exists: boolean
  storage_path: string
}

export interface DocsStatus {
  basePath: string
  entryUrl: string
  redirectUrl: string
  sourcePath: string
  distPath: string
  sourceExists: boolean
  built: boolean
  healthStatus?: 'ok' | 'warning' | 'error'
  healthMessage?: string
  actionHint?: string
  buildCommand?: string
  sourceDocumentCount: number
  builtPageCount: number
  lastSourceUpdate?: string | null
  lastBuildUpdate?: string | null
  manifestExists: boolean
  manifestSizeBytes: number
  searchIndexExists: boolean
  searchIndexSizeBytes: number
  indexCached: boolean
  indexedDocumentCount: number
  indexUpdatedAt?: string | null
  categoryCounts: Record<string, number>
  localeCounts: Record<string, number>
  recentDocuments: Array<{
    title: string
    excerpt?: string
    path: string
    href: string
    category: string
    categoryLabel: string
    locale: string
    localeLabel: string
    modified?: string | null
    size?: number
  }>
}

export interface CacheMetric {
  key: string
  name: string
  value: number
  description: string
}

export interface JobRun {
  id: number
  job_id: string
  status: string
  started_at?: string
  finished_at?: string
  duration_ms?: number
  summary?: string
  error?: string
}

export interface ScheduledJob {
  id: string
  name: string
  job_type: string
  enabled: number | boolean
  interval_seconds: number
  next_run_at?: string
  updated_at?: string
  latest_run?: JobRun | null
}

export interface ApiKeyItem {
  id: number
  name: string
  key_prefix: string
  scopes: string
  created_by?: string
  created_at?: string
  expires_at?: string | null
  last_used_at?: string | null
  revoked_at?: string | null
  is_active: number | boolean
}

export interface CreateApiKeyPayload {
  name: string
  description?: string
  scopes: string
  expires_at?: string
}

export interface ApiKeyFormValues {
  name: string
  description?: string
  scopes: string[]
  expires_at?: string
}

export interface CreateApiKeyResult extends ApiKeyItem {
  key: string
}

export interface AuditLogEntry {
  id?: number
  actor_type: string
  actor_id: string
  action: string
  target_type?: string
  target_id?: string
  detail?: string
  ip?: string
  user_agent?: string
  created_at?: string
}

export interface AuditLogResponse {
  entries: AuditLogEntry[]
  limit: number
  offset: number
}

export interface ProTableResponse<T> {
  data: T[]
  success: boolean
  total: number
}

export interface PublishDraftFormValues {
  name: string
  kind: string
  note?: string
}

export interface ThemeSettingsFormValues {
  themeMode: ThemeMode
  density: 'middle' | 'small'
}

export interface PublishIntegrityCheck {
  key: string
  title: string
  status: 'ok' | 'warning' | 'error'
  detail: string
  actionHref?: string
}

export interface PublishIntegrityPluginIssue {
  pluginId: string
  name: string
  latestVersion?: string
}

export interface PublishIntegrityReport {
  status: 'ok' | 'warning' | 'error'
  score: number
  okCount: number
  warningCount: number
  errorCount: number
  generatedAt: string
  checks: PublishIntegrityCheck[]
  app: {
    latestVersion?: string
    currentReleaseCount: number
    updatePackageCount: number
    matchedUpdateCount: number
    changelogExists: boolean
    changelogMentionsLatest: boolean
  }
  plugins: {
    total: number
    missingReadme: PublishIntegrityPluginIssue[]
    missingChangelog: PublishIntegrityPluginIssue[]
    missingPackage: PublishIntegrityPluginIssue[]
  }
  docs: {
    built: boolean
    indexedDocumentCount: number
    indexUpdatedAt?: string | null
  }
}
