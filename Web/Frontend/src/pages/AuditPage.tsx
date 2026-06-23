import { ProTable, type ProColumns } from '@ant-design/pro-components'
import { Tag, Typography } from 'antd'
import { getAuditLog } from '../services/admin'
import type { AuditLogEntry } from '../types/admin'

const columns: ProColumns<AuditLogEntry>[] = [
  {
    title: '操作',
    dataIndex: 'action',
    width: 180,
    render: (_, record) => <Tag color="blue">{record.action}</Tag>,
  },
  {
    title: '操作者',
    dataIndex: 'actor_id',
    width: 180,
    render: (_, record) => `${record.actor_type}:${record.actor_id}`,
  },
  {
    title: '目标',
    dataIndex: 'target_id',
    width: 180,
    render: (_, record) => record.target_id || record.target_type || '-',
  },
  {
    title: '详情',
    dataIndex: 'detail',
    search: false,
    render: (_, record) => <Typography.Text ellipsis>{record.detail || '-'}</Typography.Text>,
  },
  {
    title: '时间',
    dataIndex: 'created_at',
    valueType: 'dateTime',
    width: 180,
    search: false,
  },
]

export function AuditPage() {
  return (
    <ProTable<AuditLogEntry>
      rowKey={(record, index) => String(record.id ?? index)}
      columns={columns}
      request={async (params) => {
        const result = await getAuditLog({
          current: params.current,
          pageSize: params.pageSize,
          action: params.action as string | undefined,
          actor: params.actor_id as string | undefined,
          target: params.target_id as string | undefined,
        })
        return {
          data: result.entries,
          success: true,
          total: result.offset + result.entries.length + (result.entries.length >= result.limit ? result.limit : 0),
        }
      }}
      pagination={{ pageSize: 20 }}
      options={{ density: true, fullScreen: true, reload: true, setting: true }}
      cardBordered
      headerTitle="审计日志"
    />
  )
}
