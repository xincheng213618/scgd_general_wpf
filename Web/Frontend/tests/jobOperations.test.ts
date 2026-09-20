import assert from 'node:assert/strict'
import test from 'node:test'
import type { ScheduledJob } from '../src/types/admin.ts'
import {
  formatJobDuration,
  formatJobInterval,
  jobTypeLabels,
  summarizeJobs,
  filterJobs,
  jobHistoryPath,
  jobViewFromSearch,
} from '../src/utils/jobOperations.ts'

function job(overrides: Partial<ScheduledJob> = {}): ScheduledJob {
  return {
    id: 'cache_cleanup',
    name: 'Cache Cleanup',
    job_type: 'cache_cleanup',
    enabled: true,
    interval_seconds: 3600,
    run_counts: { total: 4, success: 2, error: 1, interrupted: 1, running: 0 },
    ...overrides,
  }
}

test('current job attention clears after recovery while historical failures remain available', () => {
  const recovered = job({ latest_run: { id: 7, job_id: 'cache_cleanup', status: 'success' } })
  const failed = job({ id: 'database_backup', latest_run: { id: 8, job_id: 'database_backup', status: 'error' } })
  const interrupted = job({ id: 'tool_index', enabled: false, latest_run: { id: 9, job_id: 'tool_index', status: 'interrupted' } })
  assert.deepEqual(filterJobs([recovered, failed, interrupted], 'attention').map((item) => item.id), ['database_backup', 'tool_index'])
  assert.deepEqual(filterJobs([recovered, failed, interrupted], 'disabled'), [interrupted])
  assert.equal(summarizeJobs([recovered]).failed, 1)
})

test('job links preserve the exact identifier and invalid filters fall back to all', () => {
  const path = jobHistoryPath('backup / daily&new')
  assert.equal(new URL(path, 'https://example.test').searchParams.get('job'), 'backup / daily&new')
  assert.equal(jobViewFromSearch('?view=attention&job=database_backup'), 'attention')
  assert.equal(jobViewFromSearch('?view=other'), 'all')
})

test('job intervals use readable operational units', () => {
  assert.equal(formatJobInterval(0), '仅启动时')
  assert.equal(formatJobInterval(300), '5 分钟')
  assert.equal(formatJobInterval(7200), '2 小时')
  assert.equal(formatJobInterval(172800), '2 天')
  assert.equal(formatJobInterval(45), '45 秒')
})

test('database backup jobs have an operator-facing type label', () => {
  assert.equal(jobTypeLabels.database_backup, '数据库备份')
  assert.equal(jobTypeLabels.security_cleanup, '账号安全清理')
  assert.equal(jobTypeLabels.transfer_cleanup, '临时文件清理')
})

test('job durations stay readable from milliseconds through multi-day interruptions', () => {
  assert.equal(formatJobDuration(31), '31 ms')
  assert.equal(formatJobDuration(2300), '2 秒')
  assert.equal(formatJobDuration(125000), '2 分 5 秒')
  assert.equal(formatJobDuration(7380000), '2 小时 3 分')
  assert.equal(formatJobDuration(6684118000), '77 天 8 小时')
})

test('job summary includes active and historical abnormal runs', () => {
  const summary = summarizeJobs([
    job(),
    job({
      id: 'plugin_index_check',
      enabled: false,
      run_counts: { total: 5, success: 4, error: 0, interrupted: 0, running: 1 },
    }),
  ])

  assert.deepEqual(summary, {
    total: 2,
    enabled: 1,
    running: 1,
    failed: 1,
    interrupted: 1,
  })
})
