import assert from 'node:assert/strict'
import test from 'node:test'
import {
  feedbackAgeInfo,
  feedbackStatusAction,
  feedbackStatusLabels,
  nextFeedbackStatus,
} from '../src/utils/feedback.ts'

test('feedback lifecycle follows the operator workflow', () => {
  assert.equal(nextFeedbackStatus('new'), 'in_progress')
  assert.equal(nextFeedbackStatus('in_progress'), 'resolved')
  assert.equal(nextFeedbackStatus('resolved'), null)
})

test('feedback actions use explicit localized labels', () => {
  assert.equal(feedbackStatusLabels.new, '待处理')
  assert.equal(feedbackStatusAction('new'), '标记为处理中')
  assert.equal(feedbackStatusAction('in_progress'), '标记为已解决')
  assert.equal(feedbackStatusAction('resolved'), null)
})

test('feedback age highlights unresolved backlog without mislabeling resolved items', () => {
  const now = Date.parse('2026-08-13T12:00:00Z')
  assert.deepEqual(
    feedbackAgeInfo('new', '2026-08-13T09:00:00Z', now),
    { label: '等待 3 小时', color: 'blue' },
  )
  assert.deepEqual(
    feedbackAgeInfo('in_progress', '2026-08-13T09:00:00Z', now),
    { label: '提交 3 小时', color: 'blue' },
  )
  assert.deepEqual(
    feedbackAgeInfo('new', '2026-08-05T12:00:00Z', now),
    { label: '等待 8 天', color: 'orange' },
  )
  assert.deepEqual(
    feedbackAgeInfo('in_progress', '2026-08-05T12:00:00Z', now),
    { label: '提交 8 天', color: 'orange' },
  )
  assert.deepEqual(
    feedbackAgeInfo('resolved', '2026-01-01T00:00:00Z', now),
    { label: '已解决', color: 'green' },
  )
  assert.deepEqual(feedbackAgeInfo('new', 'unknown', now), { label: '等待时间未知' })
})
