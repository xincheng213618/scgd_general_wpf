import {
  DownloadOutlined,
  FileOutlined,
  FolderOpenOutlined,
  SearchOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Alert, Button, Empty, Input, Segmented, Skeleton, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { getTools } from '../services/site'
import type { StorageItem, ToolsPayload } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

type ItemFilter = 'all' | 'directory' | 'file'

const columns: ColumnsType<StorageItem> = [
  {
    title: '名称',
    dataIndex: 'name',
    render: (value, record) => (
      <div className="file-name-cell">
        <span className={`file-type-icon ${record.is_dir ? 'folder' : 'file'}`}>
          {record.is_dir ? <FolderOpenOutlined /> : <FileOutlined />}
        </span>
        <span className="file-name-copy">
          {record.is_dir ? (
            <Link to={`/browse/${record.relative_path}`} className="file-name-link">
              {value}
            </Link>
          ) : (
            <Typography.Text strong>{value}</Typography.Text>
          )}
          <span>{record.relative_path}</span>
        </span>
      </div>
    ),
  },
  { title: '类型', dataIndex: 'is_dir', width: 90, render: (value) => <Tag>{value ? '目录' : '文件'}</Tag> },
  { title: '文件数', dataIndex: 'file_count', width: 100, render: (value) => value ?? '-' },
  { title: '大小', dataIndex: 'size', width: 120, render: (value) => humanSize(value) },
  { title: '修改时间', dataIndex: 'modified', width: 170, render: (value) => shortDate(value) },
  {
    title: '操作',
    width: 120,
    align: 'right',
    render: (_, record) => (
      <Button
        size="small"
        type={record.is_dir ? 'default' : 'primary'}
        ghost={!record.is_dir}
        icon={record.is_dir ? <FolderOpenOutlined /> : <DownloadOutlined />}
        href={record.is_dir ? `/browse/${record.relative_path}` : downloadPath(record.relative_path)}
      >
        {record.is_dir ? '浏览' : '下载'}
      </Button>
    ),
  },
]

export function ToolsPage() {
  const [data, setData] = useState<ToolsPayload | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [query, setQuery] = useState('')
  const [itemFilter, setItemFilter] = useState<ItemFilter>('all')

  useEffect(() => {
    getTools()
      .then(setData)
      .catch((err) => setError(err instanceof Error ? err.message : '工具数据加载失败'))
      .finally(() => setLoading(false))
  }, [])

  const filteredItems = useMemo(() => {
    if (!data) return []
    const keyword = query.trim().toLowerCase()
    return data.items.filter((item) => {
      const typeMatched =
        itemFilter === 'all' || (itemFilter === 'directory' && item.is_dir) || (itemFilter === 'file' && !item.is_dir)
      const keywordMatched = !keyword || `${item.name} ${item.relative_path}`.toLowerCase().includes(keyword)
      return typeMatched && keywordMatched
    })
  }, [data, itemFilter, query])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  const directoryCount = data.summary.directory_count ?? data.items.filter((item) => item.is_dir).length
  const fileCount = data.summary.file_count ?? data.items.filter((item) => !item.is_dir).length

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <section className="compact-page-hero">
        <div>
          <span className="hero-kicker light">
            <ToolOutlined />
            Tool
          </span>
          <Typography.Title level={2}>工具 / 软件</Typography.Title>
          <Typography.Paragraph>内部工具、服务包、驱动和外部软件安装包。默认展示 Tool 根目录，支持快速搜索和类型筛选。</Typography.Paragraph>
        </div>
        <div className="compact-stat-strip">
          <span>
            <strong>{fileCount}</strong>
            文件
          </span>
          <span>
            <strong>{directoryCount}</strong>
            目录
          </span>
          <span>
            <strong>{humanSize(data.summary.total_size)}</strong>
            总大小
          </span>
          <Button type="primary" ghost icon={<FolderOpenOutlined />} href="/browse/Tool">
            浏览目录
          </Button>
        </div>
      </section>

      <section className="portal-panel file-browser-panel">
        <div className="section-heading file-heading">
          <div>
            <span className="section-kicker">
              <FolderOpenOutlined />
              Tool 目录内容
            </span>
            <Typography.Paragraph>{filteredItems.length} / {data.items.length} 个项目</Typography.Paragraph>
          </div>
          <div className="file-toolbar">
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder="搜索工具、目录或压缩包"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
            <Segmented
              value={itemFilter}
              onChange={(value) => setItemFilter(value as ItemFilter)}
              options={[
                { label: '全部', value: 'all' },
                { label: '目录', value: 'directory' },
                { label: '文件', value: 'file' },
              ]}
            />
          </div>
        </div>
        <Table
          rowKey="relative_path"
          columns={columns}
          dataSource={filteredItems}
          className="file-table"
          locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="没有匹配的工具项目" /> }}
          pagination={{ pageSize: 15, showSizeChanger: true }}
          scroll={{ x: 860 }}
        />
      </section>
    </Space>
  )
}
