import assert from 'node:assert/strict'
import test from 'node:test'
import {
  formatOperationsUptime,
  operationsCapabilityLabel,
  operationsHostStatus,
  operationsScopeLabel,
  operationsSupportStatus,
  operationsTaskSource,
  operationsTaskStatus,
} from '../src/utils/operations.ts'

test('operations dashboard distinguishes relay freshness from reported status', () => {
  assert.deepEqual(operationsHostStatus(true, 'online'), { color: 'green', label: '在线' })
  assert.equal(operationsHostStatus(false, 'online').label, '未连接')
  assert.equal(operationsHostStatus(true, 'degraded').label, 'degraded')
})

test('operations activity labels preserve terminal and expiry semantics', () => {
  assert.equal(operationsTaskStatus('queued').label, '待投递')
  assert.equal(operationsTaskStatus('queued', true).label, '已过期')
  assert.equal(operationsTaskStatus('awaiting_local_consent').label, '等待本机确认')
  assert.equal(operationsTaskStatus('failed').color, 'red')
  assert.equal(operationsSupportStatus('session.active').label, '支持中')
  assert.equal(operationsSupportStatus('session.closed').label, '已关闭')
})

test('operations summaries are readable without changing unknown contracts', () => {
  assert.equal(operationsCapabilityLabel('ops.diagnostics.request'), '诊断包请求')
  assert.equal(operationsCapabilityLabel('plugin.future.capability'), 'plugin.future.capability')
  assert.equal(operationsTaskSource('device'), '配对设备')
  assert.equal(operationsTaskSource('operator'), 'Web 管理端')
  assert.equal(operationsScopeLabel('ops.jobs.create'), '创建任务')
  assert.equal(operationsScopeLabel('plugin.future.scope'), 'plugin.future.scope')
  assert.equal(formatOperationsUptime(93784), '1 天 2 小时')
  assert.equal(formatOperationsUptime(3599), '59 分钟')
})
