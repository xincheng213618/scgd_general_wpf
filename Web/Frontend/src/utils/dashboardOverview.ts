import type {
  DeploymentHistoryEntry,
  IndexStatusResponse,
  TrafficStatsResponse,
} from '../types/admin'
import { indexDefinitions } from './indexMaintenance.ts'

export type DashboardHealthLevel = 'ok' | 'warning' | 'error' | 'unknown'

export interface DashboardHealthSummary {
  level: DashboardHealthLevel
  label: string
  detail: string
}

export interface DashboardIndexSummary extends DashboardHealthSummary {
  ready: number
  total: number
  problemScopes: string[]
}

export function summarizeDashboardIndexes(
  status?: IndexStatusResponse | null,
): DashboardIndexSummary {
  const total = indexDefinitions.length
  if (!status) {
    return {
      level: 'unknown',
      label: '待加载',
      detail: '尚未取得索引运行状态',
      ready: 0,
      total,
      problemScopes: [],
    }
  }

  const problemScopes: string[] = []
  let hasError = Boolean(status.error)
  let ready = 0
  for (const definition of indexDefinitions) {
    const state = status.states[definition.scope]
    if (state?.status === 'ready' && !state.last_error) {
      ready += 1
      continue
    }
    problemScopes.push(definition.name)
    if (state?.status === 'error' || Boolean(state?.last_error)) hasError = true
  }

  if (hasError) {
    return {
      level: 'error',
      label: '存在异常',
      detail: `需要检查：${problemScopes.join('、')}`,
      ready,
      total,
      problemScopes,
    }
  }
  if (problemScopes.length > 0) {
    return {
      level: 'warning',
      label: '未全部就绪',
      detail: `待就绪：${problemScopes.join('、')}`,
      ready,
      total,
      problemScopes,
    }
  }
  return {
    level: 'ok',
    label: '全部就绪',
    detail: `${total} 项索引运行正常`,
    ready,
    total,
    problemScopes,
  }
}

export function summarizeDashboardTraffic(
  traffic?: TrafficStatsResponse | null,
): DashboardHealthSummary {
  if (!traffic) {
    return { level: 'unknown', label: '待加载', detail: '尚未取得今日访问统计' }
  }
  if (traffic.today.serverErrorResponses > 0 || traffic.recorder.lastError) {
    return {
      level: 'error',
      label: '需要排查',
      detail: traffic.recorder.lastError
        ? '访问统计记录器存在错误'
        : `今日出现 ${traffic.today.serverErrorResponses} 次服务端 5xx`,
    }
  }
  if (traffic.today.clientErrorResponses > 0 || traffic.recorder.dropped > 0) {
    return {
      level: 'warning',
      label: '有请求异常',
      detail: traffic.recorder.dropped > 0
        ? `统计记录器丢弃 ${traffic.recorder.dropped} 条记录`
        : `今日有 ${traffic.today.clientErrorResponses} 次请求侧 4xx`,
    }
  }
  return { level: 'ok', label: '访问正常', detail: '今日未记录 4xx 或 5xx' }
}

export function summarizeDashboardDeployment(
  deployment?: DeploymentHistoryEntry | null,
): DashboardHealthSummary {
  if (!deployment) {
    return { level: 'unknown', label: '暂无记录', detail: '尚无可用的 NAS 部署历史' }
  }
  if (deployment.status === 'failed') {
    return {
      level: 'error',
      label: '最近部署失败',
      detail: deployment.failure_reason || '请打开部署历史查看恢复记录',
    }
  }
  if (deployment.status === 'success' || deployment.status === 'already_current') {
    if (deployment.ready !== true) {
      return { level: 'warning', label: '就绪未确认', detail: '部署完成，但没有就绪证据' }
    }
    return {
      level: 'ok',
      label: deployment.status === 'success' ? '部署成功' : '已是最新',
      detail: '健康检查已通过',
    }
  }
  return { level: 'warning', label: deployment.status || '未知状态', detail: '请打开部署历史确认' }
}
