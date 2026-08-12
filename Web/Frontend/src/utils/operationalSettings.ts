import type { RetentionSettingsValues } from '../types/admin'

export type RetentionSettingKey = keyof RetentionSettingsValues

export interface RetentionSettingDefinition {
  key: RetentionSettingKey
  label: string
  unit: string
  description: string
  applies: string
}

export const retentionSettingDefinitions: readonly RetentionSettingDefinition[] = [
  {
    key: 'app_release_keep_count',
    label: '主程序发布包',
    unit: '个',
    description: '每次发布主程序后保留的最新版本数量。',
    applies: '下次主程序发布',
  },
  {
    key: 'plugin_package_keep_count',
    label: '插件发布包',
    unit: '个',
    description: '每个插件保留的最新安装包数量。',
    applies: '下次插件发布',
  },
  {
    key: 'access_analytics_retention_days',
    label: '访问统计',
    unit: '天',
    description: '访问趋势和聚合统计的保留周期。',
    applies: '下次访问统计清理',
  },
  {
    key: 'job_run_retention_days',
    label: '任务运行历史',
    unit: '天',
    description: '任务中心运行记录的保留周期。',
    applies: '下次任务历史清理',
  },
  {
    key: 'audit_log_retention_days',
    label: '审计日志',
    unit: '天',
    description: '管理员操作审计记录的保留周期。',
    applies: '下次管理数据清理',
  },
  {
    key: 'admin_db_backup_keep_count',
    label: '数据库备份',
    unit: '个',
    description: '管理数据库自动备份保留的最新副本数量。',
    applies: '下次备份或管理数据清理',
  },
]

export interface RetentionSettingChange extends RetentionSettingDefinition {
  before: number
  after: number
  decreasesRetention: boolean
}

export function getRetentionSettingChanges(
  before: RetentionSettingsValues,
  after: RetentionSettingsValues,
): RetentionSettingChange[] {
  return retentionSettingDefinitions
    .filter(({ key }) => before[key] !== after[key])
    .map((definition) => ({
      ...definition,
      before: before[definition.key],
      after: after[definition.key],
      decreasesRetention: after[definition.key] < before[definition.key],
    }))
}
