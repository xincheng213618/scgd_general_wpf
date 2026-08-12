export interface OperationsStatusMeta {
  color: string
  label: string
}

const taskStatuses: Record<string, OperationsStatusMeta> = {
  queued: { color: 'blue', label: '待投递' },
  delivered: { color: 'cyan', label: '已投递' },
  received: { color: 'cyan', label: '终端已接收' },
  accepted: { color: 'processing', label: '终端处理中' },
  awaiting_local_consent: { color: 'gold', label: '等待本机确认' },
  completed: { color: 'green', label: '已完成' },
  failed: { color: 'red', label: '失败' },
  rejected: { color: 'volcano', label: '终端拒绝' },
}

const supportStatuses: Record<string, OperationsStatusMeta> = {
  'session.requested': { color: 'gold', label: '等待本机同意' },
  'session.active': { color: 'green', label: '支持中' },
  'session.closed': { color: 'default', label: '已关闭' },
  'session.failed': { color: 'red', label: '失败' },
}

const capabilityLabels: Record<string, string> = {
  'ops.diagnostics.request': '诊断包请求',
  'ops.support.message': '支持消息',
  'ops.deployment.verify': '部署验证',
}

const scopeLabels: Record<string, string> = {
  'ops.capabilities.read': '读取能力目录',
  'ops.status.read': '读取运行状态',
  'ops.window.control': '控制主窗口',
  'ops.alerts.read': '读取告警',
  'ops.diagnostics.read': '读取诊断',
  'ops.diagnostics.bundle.read': '读取诊断包',
  'ops.window.snapshot.read': '读取窗口快照',
  'ops.jobs.read': '读取任务',
  'ops.jobs.create': '创建任务',
  'ops.approvals.decide': '处理审批',
  'ops.deployments.read': '读取部署',
  'ops.deployments.receipt.create': '提交部署回执',
  'ops.support.read': '读取支持会话',
  'ops.support.request': '请求远程支持',
  'ops.audit.read': '读取审计',
}

export function operationsHostStatus(online: boolean, reportedStatus: string): OperationsStatusMeta {
  if (!online) return { color: 'default', label: '未连接' }
  if (reportedStatus === 'online') return { color: 'green', label: '在线' }
  return { color: 'gold', label: reportedStatus || '状态未知' }
}

export function operationsTaskStatus(status: string, expired = false): OperationsStatusMeta {
  if (expired && ['queued', 'delivered', 'accepted'].includes(status)) {
    return { color: 'default', label: '已过期' }
  }
  return taskStatuses[status] ?? { color: 'default', label: status || '未知' }
}

export function operationsSupportStatus(state: string): OperationsStatusMeta {
  return supportStatuses[state] ?? { color: 'default', label: state || '未知' }
}

export function operationsCapabilityLabel(capabilityId: string) {
  return capabilityLabels[capabilityId] ?? capabilityId
}

export function operationsTaskSource(sourceType: string) {
  return sourceType === 'device' ? '配对设备' : 'Web 管理端'
}

export function operationsScopeLabel(scope: string) {
  return scopeLabels[scope] ?? scope
}

export function formatOperationsUptime(seconds: number) {
  const bounded = Math.max(0, Math.floor(Number(seconds) || 0))
  const days = Math.floor(bounded / 86400)
  const hours = Math.floor((bounded % 86400) / 3600)
  const minutes = Math.floor((bounded % 3600) / 60)
  if (days > 0) return `${days} 天 ${hours} 小时`
  if (hours > 0) return `${hours} 小时 ${minutes} 分钟`
  return `${minutes} 分钟`
}
