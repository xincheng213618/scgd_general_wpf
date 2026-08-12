import type {
  PublishIntegrityPluginIssue,
  PublishIntegrityReport,
} from '../types/admin'

type PluginIssueField = 'missingReadme' | 'missingChangelog' | 'missingPackage'

const pluginIssueFields: Partial<Record<string, PluginIssueField>> = {
  plugin_readme: 'missingReadme',
  plugin_changelog: 'missingChangelog',
  plugin_package: 'missingPackage',
}

export function publishIntegrityPluginIssues(
  report: PublishIntegrityReport,
  checkKey: string,
): PublishIntegrityPluginIssue[] {
  const field = pluginIssueFields[checkKey]
  return field ? report.plugins[field] : []
}

export function pluginIntegrityIssueLabel(issue: PublishIntegrityPluginIssue): string {
  const name = issue.name || issue.pluginId || '未命名插件'
  return issue.latestVersion ? `${name} · ${issue.latestVersion}` : name
}

export function pluginIntegrityIssueHref(issue: PublishIntegrityPluginIssue): string {
  const pluginId = issue.pluginId.trim()
  return pluginId ? `/plugins/${encodeURIComponent(pluginId)}` : '/plugins'
}
