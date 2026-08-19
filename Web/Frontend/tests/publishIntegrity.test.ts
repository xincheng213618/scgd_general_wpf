import assert from 'node:assert/strict'
import test from 'node:test'
import type { PublishIntegrityReport } from '../src/types/admin.ts'
import {
  pluginIntegrityIssueHref,
  pluginIntegrityIssueLabel,
  publishIntegrityPluginIssues,
} from '../src/utils/publishIntegrity.ts'

const report = {
  plugins: {
    missingReadme: [{ pluginId: 'Missing Readme', name: 'Readme 插件', latestVersion: '1.0.0' }],
    missingChangelog: [{ pluginId: 'ScreenRecorder', name: '屏幕录制', latestVersion: '1.2.1.1' }],
    missingPackage: [],
  },
} as PublishIntegrityReport

test('publish integrity maps each plugin check to its concrete affected plugins', () => {
  assert.deepEqual(
    publishIntegrityPluginIssues(report, 'plugin_changelog'),
    report.plugins.missingChangelog,
  )
  assert.deepEqual(
    publishIntegrityPluginIssues(report, 'plugin_readme'),
    report.plugins.missingReadme,
  )
  assert.deepEqual(publishIntegrityPluginIssues(report, 'installer'), [])
})

test('publish integrity issues have readable labels and safe detail links', () => {
  const issue = report.plugins.missingChangelog[0]
  assert.equal(pluginIntegrityIssueLabel(issue), '屏幕录制 · 1.2.1.1')
  assert.equal(pluginIntegrityIssueHref(issue), '/plugins/ScreenRecorder')
  assert.equal(
    pluginIntegrityIssueHref(report.plugins.missingReadme[0]),
    '/plugins/Missing%20Readme',
  )
})
