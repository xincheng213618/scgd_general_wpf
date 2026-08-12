export type ThemeMode = 'system' | 'light' | 'dark'
export type UiDensity = 'middle' | 'small'

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

export interface TrafficErrorBreakdown {
  errorResponses: number
  errorRate: number
  clientErrorResponses: number
  clientErrorRate: number
  serverErrorResponses: number
  serverErrorRate: number
  unclassifiedErrorResponses: number
  unclassifiedErrorRate: number
}

export interface TrafficSummary extends TrafficErrorBreakdown {
  periodStart: string
  periodEnd: string
  days: number
  timeZone: string
  utcOffsetMinutes: number
  calendarBoundaryEffectiveAt: string | null
  legacyCalendarDataThroughDay: string | null
  hasLegacyCalendarData: boolean
  visits: number
  uniqueVisitorDays: number
  avgResponseMs: number
  totalResponseBytes: number
}

export interface TrafficDayStats extends TrafficErrorBreakdown {
  day: string
  visits: number
  uniqueVisitors: number
  avgResponseMs: number
  maxResponseMs: number
  totalDurationMs: number
  totalResponseBytes: number
}

export interface TrafficRouteStats extends TrafficErrorBreakdown {
  route: string
  method: string
  visits: number
  avgResponseMs: number
  maxResponseMs: number
  responseBytes: number
}

export interface TrafficClientStats extends TrafficErrorBreakdown {
  client: 'desktop' | 'mobile' | 'tablet' | 'bot' | 'other'
  visits: number
  uniqueVisitorDays: number
  share: number
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

export interface SlowRequestSample {
  recorded_at: string
  method: string
  path: string
  status: number
  duration_ms: number
}

export interface PerformanceSummary {
  generated_at: string
  process_started_at: string
  threshold_ms: number
  request_buffer_count: number
  request_buffer_capacity: number
  slow_requests: SlowRequestSample[]
  slow_jobs: JobRun[]
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
  total: number
  limit: number
  offset: number
}

export interface DeploymentRetentionSummary {
  status?: string
  keep_records?: number
  before_count?: number
  after_count?: number
  removed_count?: number
  removed_successful?: number
  removed_failed?: number
  removed_bytes?: number
  preserved_unclassified?: number
  preserved_invalid?: number
}

export interface DeploymentHistoryEntry {
  sequence: number
  timestamp?: string | null
  status: string
  source?: string | null
  commit?: string | null
  previous_commit?: string | null
  backup_name?: string | null
  frontend_build?: string | null
  backend_targeted_tests?: string | null
  health?: string | null
  ready?: boolean | null
  runtime_log_verified?: boolean | null
  old_pid?: number | null
  new_pid?: number | null
  failure_reason?: string | null
  recovery: string[]
  history_retention?: DeploymentRetentionSummary | null
  backup_retention?: DeploymentRetentionSummary | null
  git_bundle_retention?: DeploymentRetentionSummary | null
}

export interface DeploymentHistoryResponse {
  entries: DeploymentHistoryEntry[]
  total: number
  limit: number
  offset: number
  summary: {
    records: number
    malformed_records: number
    retention_limit: number
    statuses: Record<string, number>
    sources: Record<string, number>
  }
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
  density: UiDensity
}

export interface UserAccount {
  id: number
  username: string
  role: 'admin' | 'user' | string
  is_active: number | boolean
  is_current?: boolean
  created_at?: string
  updated_at?: string | null
  last_login_at?: string | null
}

export type CopilotVendorType =
  | 'Custom'
  | 'DeepSeek'
  | 'OpenAI'
  | 'Claude'
  | 'Grok'
  | 'Gemini'
  | 'GLM'
  | 'MiniMax'
  | 'Xiaomi'
  | 'SenseNova'

export type CopilotProviderType = 'OpenAICompatible' | 'AnthropicCompatible'
export type CopilotReasoningMode = 'Default' | 'Disabled' | 'Enabled' | 'High' | 'Max'

export interface CopilotProfile {
  id: string
  name: string
  vendorType: CopilotVendorType
  providerType: CopilotProviderType
  baseUrl: string
  model: string
  hasApiKey: boolean
  allowInsecureHttp: boolean
  reasoningMode: CopilotReasoningMode
  enabled: boolean
  isDefault: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string
}

export interface CopilotProfilePayload {
  name: string
  vendorType: CopilotVendorType
  providerType: CopilotProviderType
  baseUrl: string
  model: string
  apiKey?: string
  allowInsecureHttp: boolean
  reasoningMode: CopilotReasoningMode
  enabled: boolean
  isDefault: boolean
  sortOrder: number
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
