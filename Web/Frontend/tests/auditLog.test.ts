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
  assert.equal(auditActionMeta('future_action').label, 'future_action')
})

test('audit actors and targets preserve unknown contract values', () => {
  assert.equal(auditActorLabel('operations_device'), '配对设备')
  assert.equal(auditActorLabel('future_actor'), 'future_actor')
  assert.equal(auditTargetLabel('scheduled_job'), '计划任务')
  assert.equal(auditTargetLabel('admin_endpoint'), '管理接口')
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
})
