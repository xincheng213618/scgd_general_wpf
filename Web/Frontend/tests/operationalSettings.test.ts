import assert from 'node:assert/strict'
import test from 'node:test'
import type { RetentionSettingsValues } from '../src/types/admin.ts'
import {
  getRetentionSettingChanges,
  retentionSettingDefinitions,
} from '../src/utils/operationalSettings.ts'

const current: RetentionSettingsValues = {
  app_release_keep_count: 5,
  plugin_package_keep_count: 3,
  access_analytics_retention_days: 90,
  job_run_retention_days: 30,
  audit_log_retention_days: 365,
  admin_db_backup_keep_count: 10,
}

test('operational settings expose the intended six safe fields in display order', () => {
  assert.deepEqual(
    retentionSettingDefinitions.map(({ key }) => key),
    [
      'app_release_keep_count',
      'plugin_package_keep_count',
      'access_analytics_retention_days',
      'job_run_retention_days',
      'audit_log_retention_days',
      'admin_db_backup_keep_count',
    ],
  )
})

test('change summary includes only edits and marks lower retention', () => {
  const changes = getRetentionSettingChanges(current, {
    ...current,
    job_run_retention_days: 14,
    audit_log_retention_days: 730,
  })

  assert.deepEqual(changes.map(({ key }) => key), [
    'job_run_retention_days',
    'audit_log_retention_days',
  ])
  assert.equal(changes[0].decreasesRetention, true)
  assert.equal(changes[1].decreasesRetention, false)
})
