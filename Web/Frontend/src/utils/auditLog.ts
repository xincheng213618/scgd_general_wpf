export interface AuditActionMeta {
  label: string
  category: string
  color: string
  security: boolean
}

export interface AuditDetailField {
  key: string
  label: string
  value: string
}

const actionDefinitions: Record<string, AuditActionMeta> = {
  auth_forbidden: { label: '权限不足', category: '安全', color: 'red', security: true },
  auth_unauthorized: { label: '未授权访问', category: '安全', color: 'red', security: true },
  login_failed: { label: '登录失败', category: '安全', color: 'volcano', security: true },
  login_throttled: { label: '登录临时锁定', category: '安全', color: 'red', security: true },
  login_throttle_unlock: { label: '解除登录限制', category: '安全', color: 'green', security: true },
  registration_throttled: { label: '注册频率受限', category: '安全', color: 'red', security: true },
  registration_throttle_clear: { label: '清除注册限制', category: '安全', color: 'green', security: true },
  password_recovery_throttled: { label: '找回密码频率受限', category: '安全', color: 'red', security: true },
  login_success: { label: '登录成功', category: '安全', color: 'green', security: false },
  user_register: { label: '用户自助注册', category: '账号', color: 'blue', security: false },
  user_create: { label: '创建用户', category: '账号', color: 'blue', security: false },
  user_profile_update: { label: '更新个人资料', category: '账号', color: 'cyan', security: false },
  user_enable: { label: '启用用户', category: '账号', color: 'green', security: false },
  user_disable: { label: '禁用用户', category: '账号', color: 'gold', security: false },
  user_delete: { label: '永久删除用户', category: '账号', color: 'red', security: true },
  user_role_update: { label: '修改用户角色', category: '账号', color: 'gold', security: false },
  user_password_reset: { label: '重置用户密码', category: '账号', color: 'gold', security: false },
  user_password_change_required: { label: '要求用户改密', category: '安全', color: 'volcano', security: true },
  user_password_recovery_request: { label: '申请找回密码', category: '安全', color: 'volcano', security: true },
  user_password_change: { label: '修改自己的密码', category: '账号', color: 'gold', security: true },
  user_session_revoke: { label: '注销登录会话', category: '安全', color: 'volcano', security: true },
  user_sessions_revoke_others: { label: '注销其他会话', category: '安全', color: 'volcano', security: true },
  user_sessions_force_revoke: { label: '管理员强制下线', category: '安全', color: 'red', security: true },
  user_bulk_security_action: { label: '批量账号安全处置', category: '安全', color: 'red', security: true },
  user_logout: { label: '退出登录', category: '安全', color: 'default', security: false },
  account_settings_update: { label: '修改账号策略', category: '设置', color: 'gold', security: false },
  retention_settings_update: { label: '修改保留策略', category: '设置', color: 'gold', security: false },
  api_key_create: { label: '创建 API Key', category: '密钥', color: 'blue', security: false },
  api_key_revoke: { label: '吊销 API Key', category: '密钥', color: 'volcano', security: false },
  api_key_rotate: { label: '轮换 API Key', category: '密钥', color: 'gold', security: false },
  cache_cleanup: { label: '清理缓存', category: '索引', color: 'cyan', security: false },
  index_refresh_all: { label: '刷新全部索引', category: '索引', color: 'cyan', security: false },
  index_refresh_plugin: { label: '刷新插件索引', category: '索引', color: 'cyan', security: false },
  index_refresh_releases: { label: '刷新版本索引', category: '索引', color: 'cyan', security: false },
  index_refresh_updates: { label: '刷新更新索引', category: '索引', color: 'cyan', security: false },
  index_refresh_tools: { label: '刷新工具索引', category: '索引', color: 'cyan', security: false },
  index_refresh_docs: { label: '刷新文档索引', category: '索引', color: 'cyan', security: false },
  db_backup: { label: '创建数据库备份', category: '备份', color: 'geekblue', security: false },
  job_run: { label: '手工运行任务', category: '任务', color: 'purple', security: false },
  job_enable: { label: '启用计划任务', category: '任务', color: 'green', security: false },
  job_disable: { label: '禁用计划任务', category: '任务', color: 'gold', security: false },
  transfer_upload: { label: '上传中转文件', category: '文件', color: 'blue', security: false },
  transfer_delete: { label: '删除中转文件', category: '文件', color: 'volcano', security: false },
  feedback_attachment_download: { label: '下载反馈附件', category: '反馈', color: 'blue', security: false },
  feedback_status_update: { label: '更新反馈状态', category: '反馈', color: 'cyan', security: false },
  copilot_profile_create: { label: '创建 Copilot 配置', category: 'Copilot', color: 'blue', security: false },
  copilot_profile_update: { label: '更新 Copilot 配置', category: 'Copilot', color: 'cyan', security: false },
  copilot_profile_delete: { label: '删除 Copilot 配置', category: 'Copilot', color: 'volcano', security: false },
  copilot_config_sync: { label: '同步 Copilot 配置', category: 'Copilot', color: 'purple', security: false },
  'operations.heartbeat': { label: '运维终端心跳', category: '终端运维', color: 'green', security: false },
  'operations.task.create': { label: '创建运维任务', category: '终端运维', color: 'purple', security: false },
  'operations.device_relay.sync': { label: '同步签名 Relay', category: '终端运维', color: 'geekblue', security: false },
  'operations.device_task.create': { label: '配对设备创建任务', category: '终端运维', color: 'purple', security: false },
}

const actorLabels: Record<string, string> = {
  user: '用户',
  user_batch: '用户批次',
  api_key: 'API Key',
  anonymous: '匿名请求',
  system: '系统',
  device: '桌面设备',
  operations_host: '运维终端',
  operations_device: '配对设备',
}

const targetLabels: Record<string, string> = {
  api: '管理接口',
  admin_endpoint: '管理接口',
  user: '用户',
  session: '登录会话',
  login_throttle: '登录限制',
  registration: '公开注册',
  password_recovery: '密码找回',
  api_key: 'API Key',
  cache_entry: '缓存',
  configuration: '配置',
  operational_settings: '运维设置',
  plugin_index: '插件索引',
  release_index: '版本索引',
  update_index: '更新索引',
  tool_index: '工具索引',
  docs_index: '文档索引',
  all_indexes: '全部索引',
  database: '数据库',
  scheduled_job: '计划任务',
  transfer_file: '中转文件',
  feedback: '反馈',
  copilot_profile: 'Copilot 配置',
  operations_host: '运维终端',
  operations_task: '运维任务',
}

const detailLabels: Record<string, string> = {
  indexed: '已索引',
  indexed_count: '已索引',
  deleted: '已删除',
  deleted_count: '已删除',
  errors: '错误',
  bytes: '字节数',
  replaced: '覆盖原文件',
  username: '用户名',
  role: '角色',
  old_role: '原角色',
  new_role: '新角色',
  status: '状态',
  sessions_invalidated: '已注销其他会话',
  sessions_revoked: '已注销会话',
  sessions_deleted: '已删除会话记录',
  must_change_password: '登录后须改密',
  failed_count: '失败次数',
  retry_after: '等待秒数',
  cleared_sources: '已清除来源',
  attempts_remaining: '剩余尝试',
  successes_remaining: '剩余注册',
  ip_address: '来源地址',
  pending_count: '处理中请求',
  request_count: '申请次数',
  recovery_requests_resolved: '已处理找回申请',
  recovery_requests_deleted: '已删除找回记录',
  added_permissions: '新增权限',
  removed_permissions: '移除权限',
  affected_active_members: '影响启用账号',
  revision: '修订号',
  cleared: '已清除',
  source: '来源',
  hostId: '终端 ID',
  capabilityId: '能力',
  deviceCount: '设备数量',
}

const detailTranslations: Array<[RegExp, (match: RegExpMatchArray) => string]> = [
  [/^Invalid credentials$/i, () => '凭据校验未通过'],
  [/^Path: (.+)$/i, (match) => `请求路径：${match[1]}`],
  [/^Insufficient scope\. Required: (.+)$/i, (match) => `权限不足，需要：${match[1]}`],
  [/^Deleted (\d+) expired entries$/i, (match) => `已删除 ${match[1]} 条过期缓存`],
  [/^Refreshed in (\d+)ms$/i, (match) => `刷新耗时 ${match[1]} ms`],
  [/^Plugin not found, marked deleted$/i, () => '插件不存在，已标记删除'],
  [/^All indexes refreshed$/i, () => '全部索引已刷新'],
  [/^Manual run: (.+)$/i, (match) => `手工运行结果：${auditValueLabel(match[1])}`],
  [/^status: (.+) -> (.+)$/i, (match) => `状态：${auditValueLabel(match[1])} → ${auditValueLabel(match[2])}`],
  [/^Backup to (.+); removed (\d+) old backup\(s\)$/i, (match) => `已备份到 ${match[1]}，删除 ${match[2]} 个旧备份`],
  [/^diagnostic attachment downloaded$/i, () => '已下载诊断附件'],
]

function auditValueLabel(value: unknown): string {
  if (value === true || value === 'true') return '是'
  if (value === false || value === 'false') return '否'
  if (value === null || value === undefined || value === '') return '-'
  const text = typeof value === 'string' ? value : JSON.stringify(value)
  return ({
    success: '成功', failed: '失败', error: '失败', enabled: '启用', disabled: '禁用',
    admin: '管理员', user: '普通用户', config: '服务配置',
  } as Record<string, string>)[text] ?? text
}

export function auditActionMeta(action: string): AuditActionMeta {
  return actionDefinitions[action] ?? {
    label: action || '未知操作',
    category: '其他',
    color: 'default',
    security: false,
  }
}

export function auditActionValueEnum() {
  return Object.fromEntries(
    Object.entries(actionDefinitions).map(([value, definition]) => [
      value,
      { text: `${definition.category} · ${definition.label}` },
    ]),
  )
}

export function auditActorLabel(actorType: string) {
  return actorLabels[actorType] ?? (actorType || '未知主体')
}

export function auditTargetLabel(targetType?: string) {
  if (!targetType) return '未指定目标'
  return targetLabels[targetType] ?? targetType
}

export function parseAuditDetail(detail?: string): AuditDetailField[] {
  const text = String(detail || '').trim()
  if (!text) return []
  try {
    const parsed = JSON.parse(text)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return Object.entries(parsed).map(([key, value]) => ({
        key,
        label: detailLabels[key] ?? key,
        value: auditValueLabel(value),
      }))
    }
  } catch {
    // Older audit records use compact key=value text instead of JSON.
  }

  const fields: AuditDetailField[] = []
  const pairPattern = /(?:^|[;\s])([A-Za-z_][A-Za-z0-9_.]*)=([^;\s]+)/g
  for (const match of text.matchAll(pairPattern)) {
    fields.push({
      key: match[1],
      label: detailLabels[match[1]] ?? match[1],
      value: auditValueLabel(match[2]),
    })
  }
  return fields
}

export function auditDetailSummary(detail?: string): string {
  const text = String(detail || '').trim()
  if (!text) return '未记录详情'
  const fields = parseAuditDetail(text)
  if (fields.length) {
    return fields.slice(0, 3).map((field) => `${field.label} ${field.value}`).join(' · ')
  }
  for (const [pattern, translate] of detailTranslations) {
    const match = text.match(pattern)
    if (match) return translate(match)
  }
  return text
}
