import assert from 'node:assert/strict'
import test from 'node:test'
import type { DatabaseBackupInventory, DeploymentHistoryEntry, IndexStatusResponse, ScheduledJob, TrafficStatsResponse, UserAccountSummary } from '../src/types/admin.ts'
import {
  summarizeDashboardAccountTasks,
  summarizeDashboardDeployment,
  summarizeDashboardIndexes,
  summarizeDashboardTraffic,
  summarizeDashboardJobs,
  summarizeDashboardBackup,
} from '../src/utils/dashboardOverview.ts'

test('dashboard distinguishes missing evidence, current job failure and recovery', () => {
  const job = { id: 'database_backup', job_type: 'database_backup', enabled: true, latest_run: { status: 'success' }, run_counts: { error: 10 } } as ScheduledJob
  assert.equal(summarizeDashboardJobs(null).level, 'unknown')
  assert.equal(summarizeDashboardJobs([{ ...job, latest_run: null }]).level, 'unknown')
  assert.equal(summarizeDashboardJobs([job]).level, 'ok')
  assert.equal(summarizeDashboardJobs([{ ...job, latest_run: { id: 1, job_id: job.id, status: 'error' } }]).level, 'error')
})

test('backup file availability does not conceal a failed or disabled automatic backup', () => {
  const inventory: DatabaseBackupInventory = { count: 1, keep_count: 3, backups: [{ name: 'backup.db', created_at: '2026-09-20T00:00:00Z', size_bytes: 20 }] }
  const job = { id: 'daily', job_type: 'database_backup', enabled: true, latest_run: { status: 'error', error: 'disk unavailable' } } as ScheduledJob
  assert.equal(summarizeDashboardBackup(null, [job]).level, 'unknown')
  assert.equal(summarizeDashboardBackup(inventory, [job]).label, '最近自动备份异常')
  assert.equal(summarizeDashboardBackup(inventory, [{ ...job, latest_run: null, enabled: false }]).label, '自动备份已停用')
  assert.equal(summarizeDashboardBackup({ ...inventory, backups: [], count: 0 }, null).label, '暂无备份文件')
  assert.equal(summarizeDashboardBackup(inventory, null).label, '已有备份文件')
  assert.match(summarizeDashboardBackup(inventory, null).detail, /不代表.*恢复/)
})

function accountSummary(passwordChanges: number, passwordRecoveries: number) {
  return {
    pending_password_changes: passwordChanges,
    pending_password_recovery: passwordRecoveries,
  } as UserAccountSummary
}

function indexStatus(statuses: Record<string, { status: string; last_error?: string }>): IndexStatusResponse {
  return { states: statuses as IndexStatusResponse['states'], counts: {} }
}

function traffic(clientErrors: number, serverErrors: number, dropped = 0, lastError: string | null = null) {
  return {
    today: { clientErrorResponses: clientErrors, serverErrorResponses: serverErrors },
    recorder: { dropped, lastError },
  } as TrafficStatsResponse
}

test('dashboard index health distinguishes ready, missing, and failed scopes', () => {
  const ready = Object.fromEntries(['plugins', 'releases', 'updates', 'tools', 'docs'].map((scope) => [scope, { status: 'ready' }]))
  assert.equal(summarizeDashboardIndexes(indexStatus(ready)).level, 'ok')
  assert.deepEqual(summarizeDashboardIndexes(indexStatus({ plugins: { status: 'ready' } })).problemScopes, [
    '应用版本', '增量包', '工具', '文档',
  ])
  assert.equal(summarizeDashboardIndexes(indexStatus({ plugins: { status: 'error', last_error: 'boom' } })).level, 'error')
  assert.equal(summarizeDashboardIndexes({ ...indexStatus(ready), error: 'refresh unavailable' }).level, 'error')
})

test('dashboard traffic health does not confuse 4xx with server failures', () => {
  assert.equal(summarizeDashboardTraffic(traffic(0, 0)).level, 'ok')
  assert.equal(summarizeDashboardTraffic(traffic(3, 0)).level, 'warning')
  assert.equal(summarizeDashboardTraffic(traffic(0, 1)).level, 'error')
  assert.equal(summarizeDashboardTraffic(traffic(0, 0, 1)).level, 'warning')
})

test('dashboard deployment health reflects the latest verified result', () => {
  assert.equal(summarizeDashboardDeployment(null).level, 'unknown')
  assert.equal(summarizeDashboardDeployment({ status: 'success', ready: true } as DeploymentHistoryEntry).level, 'ok')
  assert.equal(summarizeDashboardDeployment({ status: 'success', ready: false } as DeploymentHistoryEntry).level, 'warning')
  assert.equal(summarizeDashboardDeployment({ status: 'already_current' } as DeploymentHistoryEntry).level, 'warning')
  assert.equal(summarizeDashboardDeployment({ status: 'failed' } as DeploymentHistoryEntry).level, 'error')
})

test('dashboard account tasks surface recovery requests before routine password changes', () => {
  assert.equal(summarizeDashboardAccountTasks(null).level, 'unknown')
  assert.deepEqual(summarizeDashboardAccountTasks(accountSummary(0, 0)), {
    level: 'ok',
    label: '无需处理',
    detail: '当前没有密码找回或待改密账号',
    pending: 0,
    passwordChanges: 0,
    passwordRecoveries: 0,
  })
  assert.deepEqual(summarizeDashboardAccountTasks(accountSummary(2, 1)), {
    level: 'warning',
    label: '待处理 3',
    detail: '1 个找回申请，2 个待改密账号',
    pending: 3,
    passwordChanges: 2,
    passwordRecoveries: 1,
  })
})
