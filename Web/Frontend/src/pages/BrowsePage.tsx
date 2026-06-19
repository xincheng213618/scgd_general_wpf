import { FolderOpenOutlined } from '@ant-design/icons'
import { Alert, Breadcrumb, Button, Card, Skeleton, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { getBrowse } from '../services/site'
import type { BrowsePayload, StorageItem } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

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

  useEffect(() => {
    let mounted = true
    getBrowse(subpath, { limit, offset })
      .then((payload) => {
        if (mounted) setData(payload)
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

  const columns: ColumnsType<StorageItem> = [
    {
      title: '名称',
      dataIndex: 'name',
      render: (value, record) =>
        record.is_dir ? <Link to={`/browse/${record.relative_path}`}>{value}</Link> : <Typography.Text strong>{value}</Typography.Text>,
    },
    { title: '类型', dataIndex: 'is_dir', width: 100, render: (value) => <Tag>{value ? '目录' : '文件'}</Tag> },
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

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Breadcrumb
        items={(data.breadcrumbs || []).map(([label, href]) => ({
          title: <Link to={href}>{label}</Link>,
        }))}
      />
      <Card>
        <Tag icon={<FolderOpenOutlined />} color="blue">Storage</Tag>
        <Typography.Title level={2}>{data.subpath || 'Storage Root'}</Typography.Title>
        <Space wrap>
          <Tag>目录 {data.summary.directory_count || 0}</Tag>
          <Tag>文件 {data.summary.file_count || 0}</Tag>
          <Tag>大小 {humanSize(data.summary.total_size)}</Tag>
          {data.parent_subpath !== undefined && data.subpath && <Link to={`/browse/${data.parent_subpath}`}>返回上级</Link>}
        </Space>
      </Card>
      <Card title={`${data.items.length} 个项目`}>
        <Table
          rowKey="relative_path"
          columns={columns}
          dataSource={data.items}
          pagination={false}
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
      </Card>
    </Space>
  )
}
