import { BookOutlined, FileMarkdownOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Skeleton, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getUpdates } from '../services/site'
import type { StorageItem, UpdatePackage, UpdatesPayload } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

const updateColumns: ColumnsType<UpdatePackage> = [
  { title: '版本', dataIndex: 'version', width: 150, render: (value) => <Typography.Text strong>{value}</Typography.Text> },
  { title: '文件名', dataIndex: 'filename' },
  { title: '分支', dataIndex: 'branch', width: 120 },
  {
    title: '策略',
    dataIndex: 'fix',
    width: 120,
    render: (value) => <Tag color={value === 1 ? 'green' : 'blue'}>{value === 1 ? '.1 基线' : `fix ${value}`}</Tag>,
  },
  { title: '大小', dataIndex: 'size', width: 120, render: (value) => humanSize(value) },
  { title: '修改时间', dataIndex: 'modified', width: 180, render: (value) => shortDate(value) },
  { title: '操作', width: 100, render: (_, record) => <Button href={downloadPath(record.relative_path)}>下载</Button> },
]

const otherColumns: ColumnsType<StorageItem> = [
  { title: '名称', dataIndex: 'name' },
  { title: '类型', dataIndex: 'is_dir', width: 100, render: (value) => (value ? '目录' : '文件') },
  { title: '大小', dataIndex: 'size', width: 120, render: (value) => humanSize(value) },
  { title: '修改时间', dataIndex: 'modified', width: 180, render: (value) => shortDate(value) },
  {
    title: '操作',
    width: 120,
    render: (_, record) => (
      <Button href={record.is_dir ? `/browse/${record.relative_path}` : downloadPath(record.relative_path)}>
        {record.is_dir ? '打开' : '下载'}
      </Button>
    ),
  },
]

export function UpdatesPage() {
  const [data, setData] = useState<UpdatesPayload | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getUpdates()
      .then(setData)
      .catch((err) => setError(err instanceof Error ? err.message : '增量更新加载失败'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Tag color="blue">Update</Tag>
        <Typography.Title level={2}>增量更新</Typography.Title>
        <Typography.Paragraph type="secondary">{data.retention_note}</Typography.Paragraph>
        <Space wrap>
          <Tag icon={<ReloadOutlined />}>增量包 {data.update_summary.canonical_count || 0}</Tag>
          <Tag color="green">建议保留 {data.update_summary.retained_count || 0}</Tag>
          <Tag>最新 {data.update_summary.latest_version || '暂无'}</Tag>
          <Link to="/browse/Update">浏览 Update 目录</Link>
          <Button icon={<BookOutlined />} href="/scgd_general_wpf/02-developer-guide/deployment/auto-update">
            自动更新文档
          </Button>
          <Button icon={<FileMarkdownOutlined />} href="/changelog">
            发布说明
          </Button>
        </Space>
      </Card>
      <Card title="标准增量包">
        <Table rowKey="relative_path" columns={updateColumns} dataSource={data.update_packages} pagination={{ pageSize: 12 }} />
      </Card>
      <Card title="其它 Update 文件">
        <Table rowKey="relative_path" columns={otherColumns} dataSource={data.other_update_items} pagination={{ pageSize: 12 }} />
      </Card>
    </Space>
  )
}
