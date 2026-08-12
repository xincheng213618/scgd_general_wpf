import assert from 'node:assert/strict'
import test from 'node:test'
import { buildIndexStatusRows, indexPanelHealth } from '../src/utils/indexMaintenance.ts'
import type { IndexStatusResponse } from '../src/types/admin.ts'

function summary(overrides: Partial<IndexStatusResponse> = {}): IndexStatusResponse {
  return {
    states: {},
    counts: {},
    ...overrides,
  }
}

test('index rows use a stable operational order and public counts', () => {
  const rows = buildIndexStatusRows(summary({
    states: {
      releases: {
        scope: 'releases',
        status: 'ready',
        item_count: 4,
        duration_ms: 12,
      },
    },
    counts: { releases: 7 },
  }))

  assert.deepEqual(rows.map((row) => row.scope), ['plugins', 'releases', 'updates', 'tools', 'docs'])
  assert.equal(rows[1].indexed_count, 7)
  assert.equal(rows[1].status, 'ready')
})

test('missing index state is visible instead of treated as ready', () => {
  const rows = buildIndexStatusRows(summary())
  assert.ok(rows.every((row) => row.status === 'not_initialized'))
  assert.equal(indexPanelHealth(rows), 'warning')
})

test('recorded index errors take precedence over ready status', () => {
  const rows = buildIndexStatusRows(summary({
    states: {
      plugins: {
        scope: 'plugins',
        status: 'ready',
        last_error: 'disk unavailable',
        item_count: 1,
        duration_ms: 3,
      },
    },
  }))
  assert.equal(indexPanelHealth(rows), 'error')
})
