import type { FeedbackDetail, FeedbackStatus, FeedbackStatusUpdate } from '../types/admin'

const HOUR_MS = 60 * 60 * 1000
const DAY_MS = 24 * HOUR_MS

export function feedbackFilterFromSearch(search: string): import('../types/admin').FeedbackInboxFilter {
  const value = new URLSearchParams(search).get('status')
  return value === 'new' || value === 'in_progress' || value === 'resolved' || value === 'all' ? value : 'open'
}

export function feedbackDetailPath(pathname: string, feedbackId: string, search = ''): string {
  const params = new URLSearchParams(search)
  params.set('id', feedbackId)
  return `${pathname}?${params.toString()}`
}

export function applyFeedbackStatusUpdate(detail: FeedbackDetail | null, update: FeedbackStatusUpdate): FeedbackDetail | null {
  if (!detail || detail.feedback_id !== update.feedback_id) return detail
  // A status response is not a new detail/permission response. Keep the current drawer and access.
  return { ...detail, status: update.status, updated_at: update.updated_at }
}

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

export function feedbackDateRangeUtc(value: unknown): { createdFrom?: string, createdTo?: string } {
  if (!Array.isArray(value) || value.length !== 2) return {}
  const calendarDate = (item: unknown) => {
    if (item && typeof item === 'object' && 'format' in item && typeof item.format === 'function') {
      return item.format('YYYY-MM-DD')
    }
    return String(item || '').slice(0, 10)
  }
  const start = calendarDate(value[0])
  const end = calendarDate(value[1])
  if (!/^\d{4}-\d{2}-\d{2}$/.test(start) || !/^\d{4}-\d{2}-\d{2}$/.test(end)) return {}
  const from = new Date(`${start}T00:00:00+08:00`)
  const through = new Date(`${end}T00:00:00+08:00`)
  if (!Number.isFinite(from.getTime()) || !Number.isFinite(through.getTime())) return {}
  return {
    createdFrom: from.toISOString(),
    createdTo: new Date(through.getTime() + 24 * 60 * 60 * 1000).toISOString(),
  }
}

export function feedbackBeijingTime(value: string): string {
  const instant = Date.parse(value)
  if (!Number.isFinite(instant)) return '-'
  return `${new Date(instant + 8 * HOUR_MS).toISOString().slice(0, 19).replace('T', ' ')} BJT`
}
