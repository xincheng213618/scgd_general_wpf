import assert from 'node:assert/strict'
import test from 'node:test'
import {
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
