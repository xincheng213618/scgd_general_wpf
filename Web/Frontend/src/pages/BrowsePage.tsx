import {
  ArrowUpOutlined,
  DownloadOutlined,
  FileOutlined,
  FolderOpenOutlined,
  HomeOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { Alert, Breadcrumb, Button, Empty, Input, Segmented, Skeleton, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useMemo, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { getBrowse } from '../services/site'
import type { BrowsePayload, StorageItem } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

type ItemFilter = 'all' | 'directory' | 'file'

function browsePath(raw?: string) {
  return (raw || '').replace(/^\/+/, '')
}

export function BrowsePage() {
  const params = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const subpath = browsePath(params['*'])
  const offset = Number(searchParams.get('offset') || 0)
  const limit = Number(searchParams.get('limit') || 200)
  const [data, setData] = useState<BrowsePayload | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [query, setQuery] = useState('')
  const [itemFilter, setItemFilter] = useState<ItemFilter>('all')

  useEffect(() => {
    let mounted = true
    getBrowse(subpath, { limit, offset })
      .then((payload) => {
        if (mounted) {
          setData(payload)
          setError('')
        }
      })
      .catch((err) => {
        if (mounted) setError(err instanceof Error ? err.message : '目录加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
    }
  }, [subpath, limit, offset])

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

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Breadcrumb
        items={(data.breadcrumbs || []).map(([label, href], index) => ({
          title: (
            <Link to={href}>
              {index === 0 && <HomeOutlined />} {label}
            </Link>
          ),
        }))}
      />
      <section className="compact-page-hero">
        <div>
          <span className="hero-kicker light">
            <FolderOpenOutlined />
            Storage
          </span>
          <Typography.Title level={2}>{data.subpath || 'Storage Root'}</Typography.Title>
          <Typography.Paragraph>按真实目录浏览发布制品、插件包、工具和历史归档。</Typography.Paragraph>
        </div>
        <div className="compact-stat-strip">
          <span>
            <strong>{data.summary.directory_count || 0}</strong>
            目录
          </span>
          <span>
            <strong>{data.summary.file_count || 0}</strong>
            文件
          </span>
          <span>
            <strong>{humanSize(data.summary.total_size)}</strong>
            大小
          </span>
          {data.parent_subpath !== undefined && data.subpath && (
            <Button icon={<ArrowUpOutlined />} href={data.parent_subpath ? `/browse/${data.parent_subpath}` : '/browse'}>
              返回上级
            </Button>
          )}
        </div>
      </section>
      <section className="portal-panel file-browser-panel">
        <div className="section-heading file-heading">
          <div>
            <span className="section-kicker">
              <FolderOpenOutlined />
              {data.items.length} 个项目
            </span>
            <Typography.Paragraph>
              当前页 {offset + 1}-{Math.min(offset + data.items.length, data.total_count)} / {data.total_count}
            </Typography.Paragraph>
          </div>
          <div className="file-toolbar">
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder="搜索当前目录"
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
          pagination={false}
          locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="没有匹配的文件" /> }}
          scroll={{ x: 760 }}
        />
        {data.total_count > limit && (
          <div className="table-pager">
            <Space>
              <Button disabled={offset <= 0} onClick={() => setSearchParams({ limit: String(limit), offset: String(Math.max(0, offset - limit)) })}>
                上一页
              </Button>
              <Typography.Text type="secondary">
                {offset + 1}-{Math.min(offset + data.items.length, data.total_count)} / {data.total_count}
              </Typography.Text>
              <Button
                disabled={offset + limit >= data.total_count}
                onClick={() => setSearchParams({ limit: String(limit), offset: String(offset + limit) })}
              >
                下一页
              </Button>
            </Space>
          </div>
        )}
      </section>
    </Space>
  )
}
