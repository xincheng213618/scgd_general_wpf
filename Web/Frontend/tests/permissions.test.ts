import assert from 'node:assert/strict'
import test from 'node:test'
import {
  canOpenAdminRoute,
  changePermissionSelection,
  getAdminDashboardCapabilities,
  getAdminOperationsCapabilities,
  getAdminPublishCapabilities,
  groupAccountPermissions,
  hasPermission,
  isPermissionRevisionConflict,
  permissionUpdateSuccessMessage,
  reviewPermissionSelection,
  sessionAuthorizationKey,
  summarizePermissionChanges,
} from '../src/utils/permissions.ts'

test('registered users can enter the admin area with default role permissions', () => {
  const session = {
    authenticated: true,
    is_admin: false,
    can_access_admin: true,
    permissions: ['admin:access', 'users:manage', 'permissions:manage'],
  }

  assert.equal(hasPermission(session, 'users:manage'), true)
  assert.equal(canOpenAdminRoute(session, '/admin'), true)
  assert.equal(canOpenAdminRoute(session, '/admin/users'), true)
  assert.equal(canOpenAdminRoute(session, '/admin/login-security'), true)
  assert.equal(canOpenAdminRoute(session, '/admin/permissions'), true)
  assert.equal(canOpenAdminRoute(session, '/admin/cache'), false)
})

test('existing administrators retain full access independently of the role matrix', () => {
  const administrator = {
    authenticated: true,
    is_admin: true,
    permissions: [],
  }

  assert.equal(canOpenAdminRoute(administrator, '/admin/settings'), true)
  assert.equal(hasPermission(administrator, 'permissions:manage'), true)
  assert.equal(hasPermission({ authenticated: false }, 'admin:access'), false)
  assert.equal(canOpenAdminRoute({
    authenticated: true,
    permissions: ['cache:read'],
  }, '/admin/cache'), false)
})

test('dashboard capabilities expose only data the current role may request', () => {
  assert.deepEqual(getAdminDashboardCapabilities({
    authenticated: true,
    is_admin: false,
    permissions: ['admin:access', 'cache:read'],
  }), {
    readCache: true,
    readDeployments: false,
    readStats: false,
    readUsers: false,
  })

  assert.deepEqual(getAdminDashboardCapabilities({
    authenticated: true,
    is_admin: true,
    permissions: [],
  }), {
    readCache: true,
    readDeployments: true,
    readStats: true,
    readUsers: true,
  })

  assert.deepEqual(getAdminDashboardCapabilities({
    authenticated: true,
    is_admin: false,
    permissions: ['admin:access', 'users:manage'],
  }), {
    readCache: false,
    readDeployments: false,
    readStats: false,
    readUsers: true,
  })
})

test('operations pages separate read access from mutation capabilities', () => {
  const reader = {
    authenticated: true,
    permissions: ['admin:access', 'cache:read', 'jobs:read'],
  }
  assert.equal(canOpenAdminRoute(reader, '/admin/cache'), true)
  assert.equal(canOpenAdminRoute(reader, '/admin/jobs'), true)
  assert.deepEqual(getAdminOperationsCapabilities(reader), {
    manageBackups: false,
    readCache: true,
    readJobs: true,
    refreshCache: false,
    writeJobs: false,
  })

  const actionsWithoutRead = {
    authenticated: true,
    permissions: ['admin:access', 'cache:refresh', 'jobs:write', 'backups:manage'],
  }
  assert.equal(canOpenAdminRoute(actionsWithoutRead, '/admin/cache'), true)
  assert.equal(canOpenAdminRoute(actionsWithoutRead, '/admin/jobs'), false)
  assert.deepEqual(getAdminOperationsCapabilities(actionsWithoutRead), {
    manageBackups: true,
    readCache: false,
    readJobs: false,
    refreshCache: true,
    writeJobs: true,
  })
})

test('publish page exposes only the independently granted tools', () => {
  const transferOnly = {
    authenticated: true,
    permissions: ['admin:access', 'file:transfer'],
  }
  assert.equal(canOpenAdminRoute(transferOnly, '/admin/publish'), true)
  assert.deepEqual(getAdminPublishCapabilities(transferOnly), {
    publishPlugins: false,
    publishReleases: false,
    readIntegrity: false,
    transferFiles: true,
  })

  const integrityOnly = {
    authenticated: true,
    permissions: ['admin:access', 'stats:read'],
  }
  assert.equal(canOpenAdminRoute(integrityOnly, '/admin/publish'), true)
  assert.equal(getAdminPublishCapabilities(integrityOnly).readIntegrity, true)
})

test('permission selection review previews lockout and unusable combinations', () => {
  assert.deepEqual(reviewPermissionSelection([
    'file:transfer',
    'jobs:write',
    'users:manage',
  ]), {
    accessibleAdminRoutes: [],
    canAccessAdmin: false,
    orphanedPermissions: ['jobs:write', 'users:manage'],
    warnings: [
      '注册用户将不能进入管理后台；文件中转仍可从前台使用。',
      '已选的 2 项后台权限无法从 Web 管理端进入。',
      '“任务执行”缺少“任务查看”，Web 后台无法选择要运行或启停的任务。',
    ],
  })

  const usable = reviewPermissionSelection([
    'admin:access',
    'jobs:read',
    'jobs:write',
  ])
  assert.equal(usable.canAccessAdmin, true)
  assert.deepEqual(usable.warnings, [])
  assert.deepEqual(usable.orphanedPermissions, [])
  assert.deepEqual(usable.accessibleAdminRoutes.sort(), ['/admin', '/admin/jobs'])
})

test('authorization keys change only when account capabilities change', () => {
  const first = sessionAuthorizationKey({
    authenticated: true,
    username: 'operator',
    role: 'user',
    permissions: ['users:manage', 'admin:access', 'users:manage'],
    csrf_token: 'first-token',
  })
  const equivalent = sessionAuthorizationKey({
    authenticated: true,
    username: 'operator',
    role: 'user',
    permissions: ['admin:access', 'users:manage'],
    csrf_token: 'second-token',
  })
  const reduced = sessionAuthorizationKey({
    authenticated: true,
    username: 'operator',
    role: 'user',
    permissions: ['admin:access'],
  })

  assert.equal(first, equivalent)
  assert.notEqual(first, reduced)
  assert.equal(sessionAuthorizationKey({ authenticated: false }), '')
})

test('permission changes identify removals that can lock users out of administration', () => {
  const summary = summarizePermissionChanges(
    ['admin:access', 'cache:read', 'permissions:manage'],
    ['cache:read', 'users:manage'],
  )

  assert.deepEqual(summary.added, ['users:manage'])
  assert.deepEqual(summary.removed, ['admin:access', 'permissions:manage'])
  assert.deepEqual(summary.highRiskRemoved, ['admin:access', 'permissions:manage'])
})

test('permission update feedback uses the authoritative affected-account result', () => {
  assert.equal(permissionUpdateSuccessMessage('注册用户', {
    role: 'user',
    added: ['jobs:read'],
    removed: ['cache:read', 'cache:refresh'],
    affected_active_members: 4,
    revision: 'a'.repeat(64),
  }), '注册用户权限已更新：新增 1 项，移除 2 项；立即影响 4 个启用账号')
  assert.equal(permissionUpdateSuccessMessage('注册用户'), '注册用户权限已更新')
  assert.equal(permissionUpdateSuccessMessage('注册用户', undefined, false), (
    '注册用户权限已更新；当前登录权限刷新失败，请刷新页面后确认菜单'
  ))
})

test('permission groups can be selected and cleared without changing unrelated grants', () => {
  const selected = changePermissionSelection(
    ['admin:access', 'cache:read'],
    ['jobs:read', 'jobs:write', 'cache:read'],
    true,
  )
  assert.deepEqual(selected, ['admin:access', 'cache:read', 'jobs:read', 'jobs:write'])

  const cleared = changePermissionSelection(
    selected,
    ['cache:read', 'jobs:read', 'jobs:write'],
    false,
  )
  assert.deepEqual(cleared, ['admin:access'])
})

test('permission revision conflicts are distinguished from other request failures', () => {
  assert.equal(isPermissionRevisionConflict({
    status: 409,
    payload: { code: 'permission_revision_conflict' },
  }), true)
  assert.equal(isPermissionRevisionConflict({
    status: 409,
    payload: { code: 'administrator_permissions_are_fixed' },
  }), false)
  assert.equal(isPermissionRevisionConflict(new Error('network')), false)
})

test('account permissions keep server ordering while grouping readable metadata', () => {
  const groups = groupAccountPermissions([
    { code: 'admin:access', name: '管理后台', description: '进入后台', category: '基础', sort_order: 10 },
    { code: 'cache:read', name: '缓存查看', description: '查看缓存', category: '系统运维', sort_order: 60 },
    { code: 'jobs:read', name: '任务查看', description: '查看任务', category: '系统运维', sort_order: 80 },
  ])

  assert.deepEqual(groups.map((group) => group.category), ['基础', '系统运维'])
  assert.deepEqual(groups[1]?.permissions.map((permission) => permission.code), [
    'cache:read',
    'jobs:read',
  ])
})
