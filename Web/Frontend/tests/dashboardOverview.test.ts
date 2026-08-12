import assert from 'node:assert/strict'
import test from 'node:test'
import type { DeploymentHistoryEntry, IndexStatusResponse, TrafficStatsResponse } from '../src/types/admin.ts'
import {
  summarizeDashboardDeployment,
  summarizeDashboardIndexes,
  summarizeDashboardTraffic,
} from '../src/utils/dashboardOverview.ts'

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
