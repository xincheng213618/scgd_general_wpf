import type { FeedbackStatus } from '../types/admin'

const HOUR_MS = 60 * 60 * 1000
const DAY_MS = 24 * HOUR_MS

export interface FeedbackAgeInfo {
  label: string
  color?: string
}

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

export function feedbackAgeInfo(
  status: FeedbackStatus,
  createdAt: string,
  now = Date.now(),
): FeedbackAgeInfo {
  if (status === 'resolved') return { label: '已解决', color: 'green' }

  const created = Date.parse(createdAt)
  if (!Number.isFinite(created)) return { label: '等待时间未知' }

  const elapsed = Math.max(0, now - created)
  if (elapsed < HOUR_MS) return { label: '刚提交', color: 'blue' }
  if (elapsed < DAY_MS) {
    const hours = Math.floor(elapsed / HOUR_MS)
    return { label: status === 'new' ? `等待 ${hours} 小时` : `提交 ${hours} 小时`, color: 'blue' }
  }

  const days = Math.floor(elapsed / DAY_MS)
  const color = days >= 30 ? 'red' : days >= 7 ? 'orange' : days >= 2 ? 'gold' : 'blue'
  return {
    label: status === 'new' ? `等待 ${days} 天` : `提交 ${days} 天`,
    color,
  }
}
