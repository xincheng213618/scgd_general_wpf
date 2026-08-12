import type {
  IndexScope,
  IndexStatusResponse,
  IndexStatusRow,
} from '../types/admin'

export const indexDefinitions: ReadonlyArray<{ scope: IndexScope; name: string }> = [
  { scope: 'plugins', name: '插件' },
  { scope: 'releases', name: '应用版本' },
  { scope: 'updates', name: '增量包' },
  { scope: 'tools', name: '工具' },
  { scope: 'docs', name: '文档' },
]

export function buildIndexStatusRows(summary: IndexStatusResponse): IndexStatusRow[] {
  return indexDefinitions.map(({ scope, name }) => {
    const state = summary.states[scope]
    return {
      scope,
      name,
      status: state?.status || 'not_initialized',
      last_started_at: state?.last_started_at || null,
      last_finished_at: state?.last_finished_at || null,
      last_error: state?.last_error || '',
      item_count: Number(state?.item_count || 0),
      duration_ms: Number(state?.duration_ms || 0),
      indexed_count: Number(summary.counts[scope] ?? state?.item_count ?? 0),
    }
  })
}

export function indexPanelHealth(rows: IndexStatusRow[]): 'ok' | 'warning' | 'error' {
  if (rows.some((row) => row.status === 'error' || Boolean(row.last_error))) return 'error'
  if (rows.some((row) => row.status !== 'ready')) return 'warning'
  return 'ok'
}
