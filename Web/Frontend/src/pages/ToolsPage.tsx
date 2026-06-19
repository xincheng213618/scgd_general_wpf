import { ToolOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Skeleton, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getTools } from '../services/site'
import type { StorageItem, ToolsPayload } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

const columns: ColumnsType<StorageItem> = [
  { title: '名称', dataIndex: 'name', render: (value) => <Typography.Text strong>{value}</Typography.Text> },
  { title: '类型', dataIndex: 'is_dir', width: 100, render: (value) => <Tag>{value ? '目录' : '文件'}</Tag> },
  { title: '文件数', dataIndex: 'file_count', width: 100, render: (value) => value ?? '-' },
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

export function ToolsPage() {
  const [data, setData] = useState<ToolsPayload | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getTools()
      .then(setData)
      .catch((err) => setError(err instanceof Error ? err.message : '工具数据加载失败'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Tag icon={<ToolOutlined />} color="blue">Tool</Tag>
        <Typography.Title level={2}>工具 / 软件</Typography.Title>
        <Typography.Paragraph type="secondary">内部工具、服务包、驱动和外部软件安装包。</Typography.Paragraph>
        <Space wrap>
          <Tag>文件 {data.summary.file_count || 0}</Tag>
          <Tag>目录 {data.summary.directory_count || 0}</Tag>
          <Tag>总大小 {humanSize(data.summary.total_size)}</Tag>
          <Link to="/browse/Tool">浏览 Tool 目录</Link>
        </Space>
      </Card>
      <Card title="Tool 目录内容">
        <Table rowKey="relative_path" columns={columns} dataSource={data.items} pagination={{ pageSize: 15 }} />
      </Card>
    </Space>
  )
}
