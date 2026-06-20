export interface StorageSummary {
  item_count?: number
  directory_count?: number
  file_count?: number
  total_file_count?: number
  top_level_file_count?: number
  total_size?: number
  top_level_size?: number
}

export interface StorageItem {
  name: string
  is_dir: boolean
  path: string
  relative_path: string
  modified?: string
  modified_iso?: string
  modified_display?: string
  size?: number
  file_count?: number
}

export interface BrowsePayload {
  is_file?: boolean
  name?: string
  subpath: string
  download_url?: string
  items: StorageItem[]
  summary: StorageSummary
  total_count: number
  breadcrumbs: [string, string][]
  parent_subpath: string
  exists: boolean
  limit?: number
  offset?: number
}

export interface ReleaseArtifact {
  display_title?: string
  filename?: string
  version?: string
  relative_path: string
  kind?: string
  kind_label?: string
  era?: string
  era_label?: string
  size?: number
  modified?: string
  modified_display?: string
}

export interface AppInfo {
  latest_version?: string
  latest_release?: ReleaseArtifact
  current_count?: number
  archive_count?: number
  archive_timeline_count?: number
  archive_more_count?: number
  current_preview?: ReleaseArtifact[]
  current_releases?: ReleaseArtifact[]
  archive_recent?: ReleaseArtifact[]
  archive_timeline_groups?: ReleaseGroup[]
  release_timeline?: unknown
  changelog?: string
  changelog_html?: string
}

export interface ReleaseGroup {
  major_minor?: string
  branch?: string
  count?: number
  visible_count?: number
  kind_summary?: string
  visible_kind_summary?: string
  visible_era_summary?: string
  time_range_display?: string
  contains_archive_only_formats?: boolean
  is_expanded?: boolean
  items?: ReleaseArtifact[]
  visible_items?: ReleaseArtifact[]
}

export interface HomePayload {
  app_info: AppInfo
  overview_summary: StorageSummary
  overview_meta: Record<string, unknown>
  filesystem_spotlight: Array<{
    name: string
    label: string
    description: string
    href: string
    exists: boolean
    file_count: number
    modified: string
  }>
  recent_change_dashboard: Array<{
    title: string
    subtitle: string
    timestamp: string
    href: string
    action_label: string
    category: string
  }>
  recent_change_summary: Record<string, unknown>
  update_packages: UpdatePackage[]
  update_summary: UpdateSummary
  tool_items: StorageItem[]
  tool_summary: StorageSummary
}

export interface ReleasesPayload {
  app_info: AppInfo
  archive_visible_groups: ReleaseGroup[]
  archive_visible_group_count: number
  archive_visible_item_count: number
  release_filters: {
    major_minor: string
    branch: string
    kind: string
    era: string
    has_filters: boolean
  }
  archive_major_minor_options: SelectOption[]
  archive_branch_options: SelectOption[]
  archive_kind_options: SelectOption[]
  archive_era_options: SelectOption[]
}

export interface SelectOption {
  value: string
  label: string
  selected?: boolean
  count?: number
}

export interface UpdatePackage {
  filename: string
  version: string
  branch?: string
  fix?: number
  size?: number
  modified?: string
  relative_path: string
}

export interface UpdateSummary {
  canonical_count?: number
  retained_count?: number
  latest_version?: string
  total_size?: number
}

export interface UpdatesPayload {
  update_packages: UpdatePackage[]
  other_update_items: StorageItem[]
  update_summary: UpdateSummary
  retention_note: string
}

export interface ToolsPayload {
  items: StorageItem[]
  summary: StorageSummary
  subpath: string
  exists: boolean
}

export interface PluginSummary {
  pluginId: string
  name: string
  description?: string
  author?: string
  category?: string
  iconUrl?: string | null
  latestVersion?: string
  totalDownloads?: number
  updatedAt?: string
}

export interface PluginListResponse {
  items: PluginSummary[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  sortBy: string
  sortOrder: string
}

export interface PluginVersion {
  version: string
  requiresVersion?: string
  fileSize?: number
  fileHash?: string
  createdAt?: string
  source?: string
}

export interface PluginDetail extends PluginSummary {
  readme?: string
  changelog?: string
  currentPackageCount?: number
  historicalPackageCount?: number
  versions: PluginVersion[]
  archivedVersions: PluginVersion[]
  requiresVersion?: string
  url?: string
}

export interface UploadContext {
  max_upload_size_bytes: number
  plugin_package_keep_count: number
  supports_api_publish: boolean
  supports_html_upload: boolean
}

export interface AuthSession {
  authenticated: boolean
  is_admin?: boolean
  role?: string
  username?: string
}

export interface TransferFile {
  name: string
  size: number
  modified: string
  modified_display?: string
  download_url: string
}

export interface TransferFilesResponse {
  root: string
  files: TransferFile[]
  total_size: number
}

export interface CvwsPackage {
  fileName: string
  version: string
  suffix?: string
  size: number
  sizeText?: string
  modified?: string
  modifiedDisplay?: string
  downloadUrl: string
}

export interface CvwsContext {
  latest_version: string
  packages: CvwsPackage[]
  package_count: number
  tool_dir_display: string
}
