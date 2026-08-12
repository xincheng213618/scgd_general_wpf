import { AppstoreOutlined, SearchOutlined } from '@ant-design/icons'
import { Alert, Avatar, Button, Card, Col, Empty, Form, Input, Pagination, Row, Select, Skeleton, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { getPlugins } from '../services/site'
import type { PluginListResponse } from '../types/site'
import { shortDate } from '../utils/format'

const pluginPageSizes = [12, 24, 48]

function positiveIntegerParam(value: string | null, fallback: number) {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback
}

function pageSizeParam(value: string | null) {
  const parsed = positiveIntegerParam(value, 12)
  return pluginPageSizes.includes(parsed) ? parsed : 12
}

function sortParam(value: string | null) {
  return value === 'name' || value === 'downloads' ? value : 'updated'
}

export function PluginsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const [data, setData] = useState<PluginListResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const searchKey = searchParams.toString()

  const query = {
    keyword: searchParams.get('q') || '',
    category: searchParams.get('category') || '',
    author: searchParams.get('author') || '',
    sort: sortParam(searchParams.get('sort')),
    page: positiveIntegerParam(searchParams.get('page'), 1),
    pageSize: pageSizeParam(searchParams.get('pageSize')),
  }

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setError('')
    })
    const nextParams = new URLSearchParams(searchKey)
    const requestQuery = {
      keyword: nextParams.get('q') || '',
      category: nextParams.get('category') || '',
      author: nextParams.get('author') || '',
      sort: sortParam(nextParams.get('sort')),
      page: positiveIntegerParam(nextParams.get('page'), 1),
      pageSize: pageSizeParam(nextParams.get('pageSize')),
    }
    getPlugins({
      ...requestQuery,
      sortOrder: requestQuery.sort === 'name' ? 'asc' : 'desc',
    }, controller.signal)
      .then((plugins) => {
        if (mounted) setData(plugins)
      })
      .catch((err) => {
        if (mounted) setError(err instanceof Error ? err.message : '插件市场加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
      controller.abort()
    }
  }, [searchKey])

  const applyQuery = (values: typeof query) => {
    const next = new URLSearchParams()
    if (values.keyword) next.set('q', values.keyword)
    if (values.category) next.set('category', values.category)
    if (values.author) next.set('author', values.author)
    if (values.sort && values.sort !== 'updated') next.set('sort', values.sort)
    if (query.pageSize !== 12) next.set('pageSize', String(query.pageSize))
    setSearchParams(next)
  }

  if (loading && !data) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error && !data) return <Alert type="error" message={error} />
  if (!data) return null

  return (
    <Space direction="vertical" size={16} className="page-stack">
      {error && <Alert type="error" message={error} showIcon />}
      <Card>
        <Tag icon={<AppstoreOutlined />} color="blue">插件市场</Tag>
        <Typography.Title level={2}>插件市场</Typography.Title>
        <Typography.Paragraph type="secondary">浏览、搜索、下载插件扩展。</Typography.Paragraph>
      </Card>
      <Card className="plugin-filter-card">
        <Form
          key={searchKey}
          className="plugin-filter-form"
          layout="inline"
          initialValues={{ ...query, category: query.category || undefined }}
          onFinish={applyQuery}
        >
          <Form.Item name="keyword">
            <Input aria-label="搜索插件" prefix={<SearchOutlined />} placeholder="名称、ID、描述" allowClear />
          </Form.Item>
          <Form.Item name="category">
            <Select
              aria-label="插件分类"
              allowClear
              placeholder="分类"
              style={{ width: 150 }}
              options={data.categories.map((category) => ({ label: category, value: category }))}
            />
          </Form.Item>
          <Form.Item name="author">
            <Input aria-label="插件作者" placeholder="作者" allowClear />
          </Form.Item>
          <Form.Item name="sort">
            <Select
              aria-label="插件排序"
              style={{ width: 140 }}
              options={[
                { label: '最近更新', value: 'updated' },
                { label: '名称', value: 'name' },
                { label: '下载量', value: 'downloads' },
              ]}
            />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">搜索</Button>
              <Button onClick={() => setSearchParams({})}>清除</Button>
            </Space>
          </Form.Item>
        </Form>
      </Card>
      <div className="plugin-results-bar" role="status" aria-live="polite">
        <Typography.Text>
          共 <strong>{data.totalCount}</strong> 个插件
        </Typography.Text>
        {loading && <Typography.Text type="secondary">正在更新…</Typography.Text>}
      </div>
      {data.items.length === 0 ? (
        <Empty description="暂无匹配插件" />
      ) : (
        <Row gutter={[16, 16]}>
          {data.items.map((plugin) => (
            <Col xs={24} md={12} xl={8} key={plugin.pluginId}>
              <Card hoverable className="plugin-card">
                <Space align="start">
                  <Avatar shape="square" size={48} src={plugin.iconUrl || undefined} icon={<AppstoreOutlined />} />
                  <div className="plugin-card-main">
                    <Link to={`/plugins/${plugin.pluginId}`}>
                      <Typography.Text strong>{plugin.name}</Typography.Text>
                    </Link>
                    <div className="muted-line">{plugin.pluginId}</div>
                    <Typography.Paragraph ellipsis={{ rows: 2 }} type="secondary">
                      {plugin.description || '暂无描述'}
                    </Typography.Paragraph>
                    <Space wrap>
                      {plugin.latestVersion && <Tag color="blue">v{plugin.latestVersion}</Tag>}
                      {plugin.category && <Tag>{plugin.category}</Tag>}
                      {plugin.author && <Tag>{plugin.author}</Tag>}
                      <Tag>下载 {plugin.totalDownloads || 0}</Tag>
                    </Space>
                    <div className="card-footer-link">
                      <Typography.Text type="secondary">{shortDate(plugin.updatedAt)}</Typography.Text>
                      <Button
                        aria-label={`查看 ${plugin.name} 详情`}
                        onClick={() => navigate(`/plugins/${plugin.pluginId}`)}
                      >
                        详情
                      </Button>
                    </div>
                  </div>
                </Space>
              </Card>
            </Col>
          ))}
        </Row>
      )}
      <Card>
        <Pagination
          disabled={loading}
          current={data.page}
          pageSize={data.pageSize}
          total={data.totalCount}
          showSizeChanger
          pageSizeOptions={[12, 24, 48]}
          showTotal={(total, range) => `${range[0]}-${range[1]} / 共 ${total} 个插件`}
          onChange={(page, pageSize) => {
            const next = new URLSearchParams(searchParams)
            next.set('page', String(page))
            next.set('pageSize', String(pageSize))
            setSearchParams(next)
          }}
        />
      </Card>
    </Space>
  )
}
