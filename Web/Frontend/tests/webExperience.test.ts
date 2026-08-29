import assert from 'node:assert/strict'
import test from 'node:test'
import { createPageViewPayload, normalizeExperienceRoute } from '../src/services/webExperience.ts'

test('browser paths are reduced to the same fixed route templates as the backend', () => {
  assert.equal(normalizeExperienceRoute('/'), '/')
  assert.equal(normalizeExperienceRoute('/admin/traffic/'), '/admin/traffic')
  assert.equal(normalizeExperienceRoute('/admin/operations/hosts'), '/admin/operations/hosts')
  assert.equal(normalizeExperienceRoute('/plugins/ProjectARVRPro'), '/plugins/:pluginId')
  assert.equal(normalizeExperienceRoute('/transfer/share/0123456789abcdef0123456789abcdef'), '/transfer/share/:token')
  assert.equal(normalizeExperienceRoute('/browse/Plugins/Camera'), '/browse/*')
  assert.equal(normalizeExperienceRoute('/unknown'), null)
  assert.equal(normalizeExperienceRoute('/browse/file?token=secret'), null)
  assert.equal(normalizeExperienceRoute('//admin/traffic'), null)
})

test('page-view payloads contain only the normalized route and navigation type', () => {
  assert.deepEqual(createPageViewPayload('/plugins/Demo', 'spa'), {
    kind: 'page_view',
    route: '/plugins/:pluginId',
    navigationType: 'spa',
  })
  assert.deepEqual(Object.keys(createPageViewPayload('/admin', 'hard')!).sort(), [
    'kind',
    'navigationType',
    'route',
  ])
  assert.equal(createPageViewPayload('/private?message=secret', 'hard'), null)
})
