import assert from 'node:assert/strict'
import test from 'node:test'
import {
  feedbackAgeInfo,
  feedbackBeijingTime,
  feedbackDateRangeUtc,
  feedbackStatusAction,
  feedbackStatusLabels,
  nextFeedbackStatus,
  applyFeedbackStatusUpdate,
} from '../src/utils/feedback.ts'
import type { FeedbackDetail } from '../src/types/admin.ts'

test('status updates preserve drawer permissions and attachments without accepting stale targets', () => {
  const detail = {
    feedback_id: 'first', status: 'new', updated_at: null,
    access: { scope: 'all', can_manage: true }, attachments: [{ name: 'large.zip' }],
  } as FeedbackDetail
  const updated = applyFeedbackStatusUpdate(detail, { feedback_id: 'first', status: 'in_progress', updated_at: '2026-09-20T00:00:00Z' })!
  assert.equal(updated.status, 'in_progress')
  assert.equal(updated.access.can_manage, true)
  assert.equal(updated.attachments, detail.attachments)
  const resolved = applyFeedbackStatusUpdate(updated, { feedback_id: 'first', status: 'resolved', updated_at: '2026-09-20T00:01:00Z' })!
  assert.equal(resolved.status, 'resolved')
  assert.equal(resolved.access, detail.access)
  assert.equal(applyFeedbackStatusUpdate(detail, { feedback_id: 'other', status: 'resolved', updated_at: null }), detail)
  assert.equal(applyFeedbackStatusUpdate(null, { feedback_id: 'first', status: 'resolved', updated_at: null }), null)
})

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

test('feedback date filters use Beijing calendar-day boundaries across UTC dates', () => {
  assert.deepEqual(feedbackDateRangeUtc(['2026-09-16', '2026-09-16']), {
    createdFrom: '2026-09-15T16:00:00.000Z',
    createdTo: '2026-09-16T16:00:00.000Z',
  })
  assert.deepEqual(feedbackDateRangeUtc(undefined), {})
})

test('feedback receive time is rendered explicitly in Beijing time', () => {
  assert.equal(feedbackBeijingTime('2026-09-15T16:01:02Z'), '2026-09-16 00:01:02 BJT')
  assert.equal(feedbackBeijingTime('unknown'), '-')
})
