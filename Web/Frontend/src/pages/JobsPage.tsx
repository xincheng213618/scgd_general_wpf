import { PlayCircleOutlined } from '@ant-design/icons'
import { ProTable, type ProColumns } from '@ant-design/pro-components'
import { App, Button, Popconfirm, Space, Switch, Tag } from 'antd'
import { listJobs, runJob, setJobEnabled } from '../services/admin'
import type { ScheduledJob } from '../types/admin'

const columns: ProColumns<ScheduledJob>[] = [
  {
    title: '任务',
    dataIndex: 'name',
    copyable: true,
  },
  {
    title: '类型',
    dataIndex: 'job_type',
    width: 160,
  },
  {
    title: '启用',
    dataIndex: 'enabled',
    width: 100,
    render: (_, record) => <Tag color={record.enabled ? 'green' : 'default'}>{record.enabled ? '启用' : '禁用'}</Tag>,
  },
  {
    title: '间隔',
    dataIndex: 'interval_seconds',
    width: 120,
    renderText: (value: number) => `${value}s`,
  },
  {
    title: '最近执行',
    dataIndex: ['latest_run', 'status'],
    width: 140,
    render: (_, record) => {
      const status = record.latest_run?.status
      if (!status) return <Tag>暂无</Tag>
      return <Tag color={status === 'success' ? 'green' : status === 'error' ? 'red' : 'blue'}>{status}</Tag>
    },
  },
  {
    title: '操作',
    valueType: 'option',
    width: 230,
    render: (_, record, __, action) => (
      <Space>
        <Button
          size="small"
          icon={<PlayCircleOutlined />}
          onClick={async () => {
            await runJob(record.id)
            action?.reload()
          }}
        >
          运行
        </Button>
        <Popconfirm
          title={`确认${record.enabled ? '禁用' : '启用'}该任务？`}
          onConfirm={async () => {
            await setJobEnabled(record.id, !record.enabled)
            action?.reload()
          }}
        >
          <Switch size="small" checked={Boolean(record.enabled)} />
        </Popconfirm>
      </Space>
    ),
  },
]

export function JobsPage() {
  const { message } = App.useApp()

  return (
    <ProTable<ScheduledJob>
      rowKey="id"
      columns={columns}
      search={false}
      request={async () => {
        try {
          const data = await listJobs()
          return { data, success: true, total: data.length }
        } catch (error) {
          message.error(error instanceof Error ? error.message : '加载任务失败')
          return { data: [], success: false, total: 0 }
        }
      }}
      options={{ density: true, fullScreen: true, reload: true, setting: true }}
      cardBordered
      headerTitle="任务调度"
    />
  )
}
