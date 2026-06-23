import { ProTable, type ProColumns } from '@ant-design/pro-components'
import { Button, Popconfirm, Tag, App } from 'antd'
import { cleanupCache, getCacheStatus, refreshAllIndexes } from '../services/admin'
import type { CacheMetric } from '../types/admin'

function toMetrics(status: Awaited<ReturnType<typeof getCacheStatus>>): CacheMetric[] {
  return [
    { key: 'cache', name: '缓存条目', value: status.cache_entry_count, description: '当前缓存表总条目数' },
    { key: 'expired', name: '过期缓存', value: status.expired_cache_entry_count, description: '可安全清理的缓存' },
    { key: 'plugins', name: '插件索引', value: status.plugin_index_count, description: '插件市场索引数据' },
    { key: 'packages', name: '包索引', value: status.package_index_count, description: '插件包索引数据' },
    { key: 'releases', name: '版本索引', value: status.release_index_count, description: '应用版本制品索引' },
    { key: 'updates', name: '增量索引', value: status.update_index_count, description: 'Update 增量包索引' },
    { key: 'tools', name: '工具索引', value: status.tool_index_count, description: 'Tool 目录资源索引' },
  ]
}

const columns: ProColumns<CacheMetric>[] = [
  {
    title: '项目',
    dataIndex: 'name',
    width: 180,
  },
  {
    title: '状态',
    dataIndex: 'value',
    width: 120,
    align: 'right',
    render: (_, record) => <Tag color={record.value > 0 ? 'blue' : 'default'}>{record.value}</Tag>,
  },
  {
    title: '说明',
    dataIndex: 'description',
    search: false,
  },
]

export function CachePage() {
  const { message } = App.useApp()

  return (
    <ProTable<CacheMetric>
      rowKey="key"
      columns={columns}
      search={false}
      pagination={false}
      request={async () => {
        const status = await getCacheStatus()
        return {
          data: toMetrics(status),
          success: true,
          total: 7,
        }
      }}
      toolBarRender={(action) => [
        <Button
          key="refresh"
          type="primary"
          onClick={async () => {
            await refreshAllIndexes()
            message.success('索引刷新已完成')
            action?.reload()
          }}
        >
          刷新全部索引
        </Button>,
        <Popconfirm
          key="cleanup"
          title="确认清理过期缓存？"
          onConfirm={async () => {
            const result = await cleanupCache()
            message.success(`已清理 ${result.deleted_count} 条过期缓存`)
            action?.reload()
          }}
        >
          <Button danger>清理过期缓存</Button>
        </Popconfirm>,
      ]}
      options={{ density: true, fullScreen: true, reload: true, setting: true }}
      cardBordered
      headerTitle="缓存与索引"
      tableAlertRender={false}
      tableAlertOptionRender={false}
    />
  )
}
