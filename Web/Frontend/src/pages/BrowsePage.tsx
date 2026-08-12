import {
  ArrowUpOutlined,
  DownloadOutlined,
  FileOutlined,
  FolderOpenOutlined,
  HomeOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { Alert, Breadcrumb, Button, Card, Empty, Input, Segmented, Skeleton, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useDeferredValue, useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { getBrowse } from '../services/site'
import type { BrowsePayload, StorageItem } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

type ItemFilter = 'all' | 'directory' | 'file'

function browsePath(raw?: string) {
  return (raw || '').replace(/^\/+/, '')
}

function integerParam(value: string | null, fallback: number, minimum: number, maximum: number) {
  const parsed = Number(value || fallback)
  return Number.isInteger(parsed) ? Math.max(minimum, Math.min(maximum, parsed)) : fallback
}

function itemFilterParam(value: string | null): ItemFilter {
  return value === 'directory' || value === 'file' ? value : 'all'
}

export function BrowsePage() {
  const params = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const subpath = browsePath(params['*'])
  const offset = integerParam(searchParams.get('offset'), 0, 0, 100000)
  const limit = integerParam(searchParams.get('limit'), 200, 1, 1000)
  const query = searchParams.get('q') || ''
  const itemFilter = itemFilterParam(searchParams.get('type'))
  const deferredQuery = useDeferredValue(query)
  const [data, setData] = useState<BrowsePayload | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setError('')
    })
    getBrowse(subpath, { limit, offset, q: deferredQuery, type: itemFilter }, controller.signal)
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
      controller.abort()
    }
  }, [subpath, limit, offset, deferredQuery, itemFilter])

  const updateFilters = (nextQuery: string, nextType: ItemFilter) => {
    const next = new URLSearchParams(searchParams)
    if (nextQuery) next.set('q', nextQuery)
    else next.delete('q')
    if (nextType !== 'all') next.set('type', nextType)
    else next.delete('type')
    next.delete('offset')
    setSearchParams(next, { replace: true })
  }

  const updateOffset = (nextOffset: number) => {
    const next = new URLSearchParams(searchParams)
    next.set('limit', String(limit))
    if (nextOffset > 0) next.set('offset', String(nextOffset))
    else next.delete('offset')
    setSearchParams(next)
  }

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
    { title: '修改时间', dataIndex: 'modified', width: 170, render: (value, record) => shortDate(record.modified_iso || value) },
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

  if (loading && !data) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error && !data) return <Alert type="error" message={error} />
  if (!data) return null

  if (data.is_file) {
    const parentSubpath = data.subpath.split('/').slice(0, -1).join('/')
    return (
      <Space direction="vertical" size={16} className="page-stack">
        <Card>
          <Tag icon={<FileOutlined />} color="blue">File</Tag>
          <Typography.Title level={2}>{data.name || data.subpath}</Typography.Title>
          <Typography.Paragraph type="secondary">{data.subpath}</Typography.Paragraph>
          <Space wrap>
            <Button type="primary" icon={<DownloadOutlined />} href={data.download_url}>
              下载文件
            </Button>
            <Link to={parentSubpath ? `/browse/${parentSubpath}` : '/browse'}>返回所在目录</Link>
          </Space>
        </Card>
      </Space>
    )
  }

  const rangeStart = data.items.length > 0 ? offset + 1 : 0
  const rangeEnd = Math.min(offset + data.items.length, data.total_count)

  return (
    <Space direction="vertical" size={16} className="page-stack">
      {error && <Alert type="error" message={error} showIcon />}
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
            当前页目录
          </span>
          <span>
            <strong>{data.summary.file_count || 0}</strong>
            当前页文件
          </span>
          <span>
            <strong>{humanSize(data.summary.total_size)}</strong>
            当前页大小
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
              {data.total_count} 个匹配项
            </span>
            <Typography.Paragraph>
              当前页 {rangeStart}-{rangeEnd} / {data.total_count}
              {(data.query || data.item_type !== 'all') && ` · 目录共 ${data.available_count} 项`}
            </Typography.Paragraph>
          </div>
          <div className="file-toolbar">
            <Input
              allowClear
              prefix={<SearchOutlined />}
              placeholder="搜索整个目录"
              maxLength={100}
              value={query}
              onChange={(event) => updateFilters(event.target.value, itemFilter)}
            />
            <Segmented
              value={itemFilter}
              onChange={(value) => updateFilters(query, value as ItemFilter)}
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
          dataSource={data.items}
          loading={loading}
          className="file-table"
          pagination={false}
          locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="没有匹配的文件" /> }}
          scroll={{ x: 760 }}
        />
        {(data.total_count > limit || offset > 0) && (
          <div className="table-pager">
            <Space>
              <Button disabled={offset <= 0} onClick={() => updateOffset(Math.max(0, offset - limit))}>
                上一页
              </Button>
              <Typography.Text type="secondary">
                {rangeStart}-{rangeEnd} / {data.total_count}
              </Typography.Text>
              <Button
                disabled={offset + limit >= data.total_count}
                onClick={() => updateOffset(offset + limit)}
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
