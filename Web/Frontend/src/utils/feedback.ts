import type { FeedbackStatus } from '../types/admin'

export const feedbackStatusLabels: Record<FeedbackStatus, string> = {
  new: '待处理',
  in_progress: '处理中',
  resolved: '已解决',
}

export const feedbackStatusColors: Record<FeedbackStatus, string> = {
  new: 'red',
  in_progress: 'gold',
  resolved: 'green',
}

export function nextFeedbackStatus(status: FeedbackStatus): FeedbackStatus | null {
  if (status === 'new') return 'in_progress'
  if (status === 'in_progress') return 'resolved'
  return null
}

export function feedbackStatusAction(status: FeedbackStatus): string | null {
  const next = nextFeedbackStatus(status)
  return next ? `标记为${feedbackStatusLabels[next]}` : null
}
