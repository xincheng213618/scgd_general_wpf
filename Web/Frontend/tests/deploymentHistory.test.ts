import assert from 'node:assert/strict'
import test from 'node:test'
import type { DeploymentHistoryEntry } from '../src/types/admin.ts'
import {
  deploymentCheckDisplay,
  deploymentFailureDisplay,
  deploymentNotice,
  deploymentRecoveryDisplay,
  deploymentSourceDisplay,
  deploymentStatusDisplay,
} from '../src/utils/deploymentHistory.ts'

function entry(values: Partial<DeploymentHistoryEntry>): DeploymentHistoryEntry {
  return { sequence: 1, status: 'success', recovery: [], ...values }
}

test('deployment labels localize known values and preserve unknown contracts', () => {
  assert.equal(deploymentStatusDisplay('already_current').text, '已是当前版本')
  assert.equal(deploymentStatusDisplay('future_status').text, 'future_status')
  assert.equal(deploymentSourceDisplay('git_bundle'), 'Git Bundle')
  assert.equal(deploymentSourceDisplay(null), '早期记录')
  assert.equal(deploymentFailureDisplay('source_control'), '代码同步')
  assert.equal(deploymentRecoveryDisplay('restored_previous_frontend'), '已恢复部署前的前端版本')
  assert.equal(deploymentRecoveryDisplay('future_recovery'), 'future_recovery')
})

test('deployment notice distinguishes verified, incomplete, and failed runs', () => {
  assert.equal(deploymentNotice(entry({ ready: true, health: 'ok' })).tone, 'success')
  assert.equal(deploymentNotice(entry({ ready: null, health: 'ok' })).tone, 'warning')
  assert.equal(deploymentNotice(entry({ status: 'failed', failure_reason: 'tests' })).tone, 'error')
  assert.match(deploymentNotice(entry({ status: 'failed', recovery: ['removed_staged_frontend'] })).description, /1 项恢复动作/)
})

test('deployment check display does not treat missing evidence as success', () => {
  assert.deepEqual(deploymentCheckDisplay('passed', ['passed']), { color: 'green', text: '通过' })
  assert.equal(deploymentCheckDisplay(undefined, ['passed']).text, '未记录')
  assert.equal(deploymentCheckDisplay(false, [true]).color, 'red')
  assert.equal(deploymentCheckDisplay('skipped', ['passed']).color, 'gold')
})
