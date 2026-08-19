import type { DeploymentHistoryEntry } from '../types/admin'

export type DeploymentTone = 'success' | 'warning' | 'error' | 'default'

export interface DeploymentDisplay {
  color: string
  text: string
}

export interface DeploymentNotice {
  tone: Exclude<DeploymentTone, 'default'>
  title: string
  description: string
}

export const deploymentStatusLabels: Record<string, DeploymentDisplay> = {
  success: { color: 'green', text: '成功' },
  failed: { color: 'red', text: '失败' },
  already_current: { color: 'blue', text: '已是当前版本' },
}

export const deploymentFailureLabels: Record<string, string> = {
  source_control: '代码同步',
  frontend_build: '前端构建',
  tests: '自动测试',
  service_health: '服务健康',
  backup: '备份',
  deployment: '部署流程',
}

const deploymentSourceLabels: Record<string, string> = {
  origin: 'Git 远端',
  git_bundle: 'Git Bundle',
  legacy: '早期记录',
}

const deploymentRecoveryLabels: Record<string, string> = {
  removed_staged_frontend: '已移除未启用的前端构建目录',
  restored_previous_frontend: '已恢复部署前的前端版本',
  service_restart_attempted: '已尝试重新启动 Web 服务',
  recovery_failed: '自动恢复未能完整执行',
}

export function deploymentStatusDisplay(status: string): DeploymentDisplay {
  return deploymentStatusLabels[status] ?? { color: 'default', text: status || '未知状态' }
}

export function deploymentSourceDisplay(source?: string | null) {
  const value = source || 'legacy'
  return deploymentSourceLabels[value] || value
}

export function deploymentFailureDisplay(reason?: string | null) {
  if (!reason) return '未知阶段'
  return deploymentFailureLabels[reason] || reason
}

export function deploymentRecoveryDisplay(code: string) {
  return deploymentRecoveryLabels[code] || code
}

export function deploymentNotice(entry: DeploymentHistoryEntry): DeploymentNotice {
  if (entry.status === 'failed') {
    const recoveryCount = entry.recovery.length
    return {
      tone: 'error',
      title: `部署在“${deploymentFailureDisplay(entry.failure_reason)}”阶段失败`,
      description: recoveryCount > 0
        ? `系统记录了 ${recoveryCount} 项恢复动作；原始错误与服务器路径已隐藏。`
        : '没有记录自动恢复动作；原始错误与服务器路径已隐藏。',
    }
  }
  if (entry.ready === true && entry.health === 'ok') {
    return {
      tone: 'success',
      title: entry.status === 'already_current' ? '当前版本健康' : '部署验证通过',
      description: '健康检查与就绪检查均已通过。',
    }
  }
  return {
    tone: 'warning',
    title: '部署完成，但验证证据不完整',
    description: '请核对构建、测试、健康检查、就绪状态与运行日志。',
  }
}

export function deploymentCheckDisplay(
  value: string | boolean | null | undefined,
  successValues: ReadonlyArray<string | boolean>,
) {
  if (value === null || value === undefined || value === '') {
    return { color: 'default', text: '未记录' }
  }
  if (successValues.includes(value)) {
    const successLabels: Record<string, string> = { success: '成功', passed: '通过', ok: '正常' }
    return { color: 'green', text: typeof value === 'boolean' ? '已确认' : successLabels[value] || value }
  }
  if (value === 'skipped') return { color: 'gold', text: '已跳过' }
  if (value === false) return { color: 'red', text: '未确认' }
  const failureLabels: Record<string, string> = { failed: '失败', error: '异常' }
  return { color: 'red', text: failureLabels[String(value)] || String(value) }
}
