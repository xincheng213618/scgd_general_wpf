import type { ScheduledJob } from '../types/admin'

export const jobStatusMeta: Record<string, { color: string; label: string }> = {
  success: { color: 'green', label: '成功' },
  error: { color: 'red', label: '失败' },
  running: { color: 'blue', label: '运行中' },
  interrupted: { color: 'gold', label: '已中断' },
  skipped: { color: 'default', label: '已跳过' },
}

export const jobTypeLabels: Record<string, string> = {
  index_check: '索引检查',
  startup_check: '启动检查',
  cache_cleanup: '缓存清理',
  security_cleanup: '账号安全清理',
  transfer_cleanup: '临时文件清理',
  analytics_retention: '访问统计保留',
  history_retention: '运行历史保留',
  data_retention: '管理数据保留',
  database_backup: '数据库备份',
}

export function formatJobInterval(seconds: number) {
  const interval = Number(seconds || 0)
  if (interval <= 0) return '仅启动时'
  if (interval % 86400 === 0) return `${interval / 86400} 天`
  if (interval % 3600 === 0) return `${interval / 3600} 小时`
  if (interval % 60 === 0) return `${interval / 60} 分钟`
  return `${interval} 秒`
}

export function formatJobDuration(milliseconds?: number) {
  const duration = Math.max(0, Number(milliseconds || 0))
  if (duration < 1000) return `${Math.round(duration)} ms`

  const totalSeconds = Math.round(duration / 1000)
  if (totalSeconds < 60) return `${totalSeconds} 秒`

  const totalMinutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  if (totalMinutes < 60) return `${totalMinutes} 分${seconds ? ` ${seconds} 秒` : ''}`

  const totalHours = Math.floor(totalMinutes / 60)
  const minutes = totalMinutes % 60
  if (totalHours < 24) return `${totalHours} 小时${minutes ? ` ${minutes} 分` : ''}`

  const days = Math.floor(totalHours / 24)
  const hours = totalHours % 24
  return `${days} 天${hours ? ` ${hours} 小时` : ''}`
}

export function summarizeJobs(jobs: ScheduledJob[]) {
  return jobs.reduce(
    (summary, job) => ({
      total: summary.total + 1,
      enabled: summary.enabled + (job.enabled ? 1 : 0),
      running: summary.running + Number(job.run_counts?.running || 0),
      failed: summary.failed + Number(job.run_counts?.error || 0),
      interrupted: summary.interrupted + Number(job.run_counts?.interrupted || 0),
    }),
    { total: 0, enabled: 0, running: 0, failed: 0, interrupted: 0 },
  )
}
