import type { RolePermissionChange } from '../types/admin'
import type { AccountPermissionDefinition, AuthSession } from '../types/site'

const highRiskPermissions = new Set(['admin:access', 'permissions:manage'])

export interface PermissionChangeSummary {
  added: string[]
  removed: string[]
  highRiskRemoved: string[]
}

export interface PermissionSelectionReview {
  accessibleAdminRoutes: string[]
  canAccessAdmin: boolean
  orphanedPermissions: string[]
  warnings: string[]
}

export interface AccountPermissionGroup {
  category: string
  permissions: AccountPermissionDefinition[]
}

export interface AdminDashboardCapabilities {
  readCache: boolean
  readDeployments: boolean
  readStats: boolean
  readUsers: boolean
}

export interface AdminOperationsCapabilities {
  manageBackups: boolean
  readCache: boolean
  readJobs: boolean
  refreshCache: boolean
  writeJobs: boolean
}

export interface AdminPublishCapabilities {
  publishPlugins: boolean
  publishReleases: boolean
  readIntegrity: boolean
  transferFiles: boolean
}

export function groupAccountPermissions(
  permissions: AccountPermissionDefinition[],
): AccountPermissionGroup[] {
  const grouped = new Map<string, AccountPermissionDefinition[]>()
  permissions.forEach((permission) => {
    const items = grouped.get(permission.category) ?? []
    items.push(permission)
    grouped.set(permission.category, items)
  })
  return [...grouped.entries()].map(([category, items]) => ({
    category,
    permissions: items,
  }))
}

export function changePermissionSelection(
  currentPermissions: string[],
  targetPermissions: string[],
  checked: boolean,
): string[] {
  const next = new Set(currentPermissions)
  targetPermissions.forEach((code) => {
    if (checked) next.add(code)
    else next.delete(code)
  })
  return [...next].sort()
}

export function isPermissionRevisionConflict(error: unknown): boolean {
  if (!error || typeof error !== 'object') return false
  const candidate = error as { status?: unknown, payload?: unknown }
  if (candidate.status !== 409 || !candidate.payload || typeof candidate.payload !== 'object') {
    return false
  }
  return (candidate.payload as { code?: unknown }).code === 'permission_revision_conflict'
}

export function summarizePermissionChanges(
  currentPermissions: string[],
  nextPermissions: string[],
): PermissionChangeSummary {
  const current = new Set(currentPermissions)
  const next = new Set(nextPermissions)
  const added = [...next].filter((code) => !current.has(code)).sort()
  const removed = [...current].filter((code) => !next.has(code)).sort()
  return {
    added,
    removed,
    highRiskRemoved: removed.filter((code) => highRiskPermissions.has(code)),
  }
}

export function permissionUpdateSuccessMessage(
  roleName: string,
  change?: RolePermissionChange,
  sessionRefreshed = true,
): string {
  const result = change
    ? `${roleName}权限已更新：新增 ${change.added.length} 项，移除 ${change.removed.length} 项；立即影响 ${change.affected_active_members} 个启用账号`
    : `${roleName}权限已更新`
  return sessionRefreshed
    ? result
    : `${result}；当前登录权限刷新失败，请刷新页面后确认菜单`
}

export function hasPermission(session: AuthSession | null, ...required: string[]): boolean {
  if (!session?.authenticated) return false
  if (session.is_admin) return true
  const granted = new Set(session.permissions ?? [])
  return granted.has('admin:*') || required.some((permission) => granted.has(permission))
}

export function getAdminDashboardCapabilities(
  session: AuthSession | null,
): AdminDashboardCapabilities {
  return {
    readCache: hasPermission(session, 'cache:read'),
    readDeployments: hasPermission(session, 'deployments:read'),
    readStats: hasPermission(session, 'stats:read'),
    readUsers: hasPermission(session, 'users:manage'),
  }
}

export function getAdminOperationsCapabilities(
  session: AuthSession | null,
): AdminOperationsCapabilities {
  return {
    manageBackups: hasPermission(session, 'backups:manage'),
    readCache: hasPermission(session, 'cache:read'),
    readJobs: hasPermission(session, 'jobs:read'),
    refreshCache: hasPermission(session, 'cache:refresh'),
    writeJobs: hasPermission(session, 'jobs:write'),
  }
}

export function getAdminPublishCapabilities(
  session: AuthSession | null,
): AdminPublishCapabilities {
  return {
    publishPlugins: hasPermission(session, 'plugin:publish'),
    publishReleases: hasPermission(session, 'release:publish'),
    readIntegrity: hasPermission(session, 'stats:read'),
    transferFiles: hasPermission(session, 'file:transfer'),
  }
}

export function sessionAuthorizationKey(session: AuthSession | null): string {
  if (!session?.authenticated) return ''
  return JSON.stringify([
    session.username ?? '',
    session.role ?? '',
    session.is_admin === true,
    session.can_access_admin === true,
    session.must_change_password === true,
    [...new Set(session.permissions ?? [])].sort(),
  ])
}

export const adminRoutePermissions: Record<string, string[]> = {
  '/admin': ['admin:access'],
  '/admin/publish': ['plugin:publish', 'release:publish', 'file:transfer', 'stats:read'],
  '/admin/files': ['files:manage'],
  '/admin/cache': ['cache:read', 'cache:refresh', 'backups:manage'],
  '/admin/jobs': ['jobs:read'],
  '/admin/deployments': ['deployments:read'],
  '/admin/operations/hosts': ['operations:manage'],
  '/admin/feedback': ['feedback:manage'],
  '/admin/users': ['users:manage'],
  '/admin/login-security': ['users:manage'],
  '/admin/permissions': ['permissions:manage'],
  '/admin/api-keys': ['api_keys:manage'],
  '/admin/copilot': ['copilot:manage'],
  '/admin/audit': ['audit:read'],
  '/admin/traffic': ['stats:read'],
  '/admin/settings': ['settings:manage'],
}

export function canOpenAdminRoute(session: AuthSession | null, path: string): boolean {
  if (!hasPermission(session, 'admin:access')) return false
  const required = adminRoutePermissions[path] ?? ['admin:access']
  return hasPermission(session, ...required)
}

export function reviewPermissionSelection(
  permissionCodes: string[],
): PermissionSelectionReview {
  const selected = new Set(permissionCodes)
  const previewSession: AuthSession = {
    authenticated: true,
    is_admin: false,
    permissions: [...selected],
  }
  const canAccessAdmin = hasPermission(previewSession, 'admin:access')
  const accessibleAdminRoutes = canAccessAdmin
    ? Object.keys(adminRoutePermissions).filter((path) => canOpenAdminRoute(previewSession, path))
    : []
  const orphanedPermissions = canAccessAdmin
    ? []
    : [...selected].filter((code) => code !== 'admin:access' && code !== 'admin:*' && code !== 'file:transfer').sort()
  const warnings: string[] = []

  if (selected.size === 0) {
    warnings.push('注册用户将只保留登录、个人中心和账号安全能力。')
  } else if (!canAccessAdmin) {
    warnings.push(selected.has('file:transfer')
      ? '注册用户将不能进入管理后台；文件中转仍可从前台使用。'
      : '注册用户将不能进入管理后台。')
    if (orphanedPermissions.length > 0) {
      warnings.push(`已选的 ${orphanedPermissions.length} 项后台权限无法从 Web 管理端进入。`)
    }
  }
  if (selected.has('jobs:write') && !selected.has('jobs:read')) {
    warnings.push('“任务执行”缺少“任务查看”，Web 后台无法选择要运行或启停的任务。')
  }

  return {
    accessibleAdminRoutes,
    canAccessAdmin,
    orphanedPermissions,
    warnings,
  }
}
