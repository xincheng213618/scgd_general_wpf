import type { ApiKeyAuditActivityItem, ApiKeyScopeDefinition } from '../types/admin'

export interface ApiKeyScopeOption {
  label: string
  value: string
  title: string
}

export interface ApiKeyScopeOptionGroup {
  label: string
  options: ApiKeyScopeOption[]
}

export function groupApiKeyScopeOptions(
  definitions: ApiKeyScopeDefinition[],
): ApiKeyScopeOptionGroup[] {
  const groups = new Map<string, ApiKeyScopeOption[]>()
  definitions.forEach((definition) => {
    const options = groups.get(definition.category) ?? []
    options.push({
      label: `${definition.value} · ${definition.label}`,
      value: definition.value,
      title: definition.description,
    })
    groups.set(definition.category, options)
  })
  return [...groups].map(([label, options]) => ({ label, options }))
}

export function apiKeyAuditTarget(item: ApiKeyAuditActivityItem): string {
  if (item.target_id) return item.target_id
  if (item.target_type) return item.target_type
  return '-'
}
