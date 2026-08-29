import assert from 'node:assert/strict'
import test from 'node:test'
import {
  auditActionMeta,
  auditActorLabel,
  auditDetailSummary,
  auditTargetLabel,
  parseAuditDetail,
} from '../src/utils/auditLog.ts'

test('audit metadata makes security and operational actions readable', () => {
  assert.deepEqual(auditActionMeta('auth_unauthorized'), {
    label: '未授权访问', category: '安全', color: 'red', security: true,
  })
  assert.equal(auditActionMeta('operations.device_relay.sync').label, '同步签名 Relay')
  assert.equal(auditActionMeta('login_success').label, '登录成功')
  assert.equal(auditActionMeta('login_throttled').label, '登录临时锁定')
  assert.equal(auditActionMeta('login_throttle_unlock').label, '解除登录限制')
  assert.equal(auditActionMeta('registration_throttled').label, '注册频率受限')
  assert.equal(auditActionMeta('registration_throttle_clear').label, '清除注册限制')
  assert.equal(auditActionMeta('password_recovery_throttled').label, '找回密码频率受限')
  assert.equal(auditActionMeta('user_profile_update').label, '更新个人资料')
  assert.equal(auditActionMeta('user_delete').label, '永久删除用户')
  assert.equal(auditActionMeta('user_sessions_force_revoke').label, '管理员强制下线')
  assert.equal(auditActionMeta('user_password_change_required').label, '要求用户改密')
  assert.equal(auditActionMeta('user_password_recovery_request').label, '申请找回密码')
  assert.equal(auditActionMeta('user_bulk_security_action').label, '批量账号安全处置')
  assert.equal(auditActionMeta('future_action').label, 'future_action')
})

test('audit actors and targets preserve unknown contract values', () => {
  assert.equal(auditActorLabel('operations_device'), '配对设备')
  assert.equal(auditActorLabel('future_actor'), 'future_actor')
  assert.equal(auditTargetLabel('scheduled_job'), '计划任务')
  assert.equal(auditTargetLabel('admin_endpoint'), '管理接口')
  assert.equal(auditTargetLabel('login_throttle'), '登录限制')
  assert.equal(auditTargetLabel('registration'), '公开注册')
  assert.equal(auditTargetLabel('password_recovery'), '密码找回')
  assert.equal(auditTargetLabel('future_target'), 'future_target')
})

test('audit details support JSON, key-value, and legacy sentences', () => {
  assert.deepEqual(parseAuditDetail('{"hostId":"host-1","deviceCount":2}'), [
    { key: 'hostId', label: '终端 ID', value: 'host-1' },
    { key: 'deviceCount', label: '设备数量', value: '2' },
  ])
  assert.equal(auditDetailSummary('indexed=12 deleted=3 errors=0'), '已索引 12 · 已删除 3 · 错误 0')
  assert.equal(auditDetailSummary('Deleted 4 expired entries'), '已删除 4 条过期缓存')
  assert.equal(auditDetailSummary('Manual run: success'), '手工运行结果：成功')
  assert.equal(auditDetailSummary('Path: /api/admin/users'), '请求路径：/api/admin/users')
  assert.equal(auditDetailSummary('status: open -> resolved'), '状态：open → resolved')
  assert.equal(auditDetailSummary('Invalid credentials'), '凭据校验未通过')
  assert.equal(
    auditDetailSummary('failed_count=5 retry_after=900'),
    '失败次数 5 · 等待秒数 900',
  )
  assert.equal(
    auditDetailSummary('username=worker;cleared_sources=5'),
    '用户名 worker · 已清除来源 5',
  )
  assert.equal(
    auditDetailSummary('username=worker;sessions_revoked=2'),
    '用户名 worker · 已注销会话 2',
  )
  assert.equal(
    auditDetailSummary('username=worker;must_change_password=true'),
    '用户名 worker · 登录后须改密 是',
  )
  assert.equal(
    auditDetailSummary('username=worker;request_count=2;recovery_requests_resolved=1'),
    '用户名 worker · 申请次数 2 · 已处理找回申请 1',
  )
  assert.equal(
    auditDetailSummary('added_permissions=jobs:read;removed_permissions=cache:read;affected_active_members=3;revision=abc'),
    '新增权限 jobs:read · 移除权限 cache:read · 影响启用账号 3',
  )
  assert.equal(
    auditDetailSummary('ip_address=198.51.100.90;cleared=true;pending_count=0'),
    '来源地址 198.51.100.90 · 已清除 是 · 处理中请求 0',
  )
  assert.equal(auditDetailSummary('role=admin source=config'), '角色 管理员 · 来源 服务配置')
})
