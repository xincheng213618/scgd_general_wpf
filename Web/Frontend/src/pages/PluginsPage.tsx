import { AppstoreOutlined, SearchOutlined } from '@ant-design/icons'
import { Alert, Avatar, Button, Card, Col, Empty, Form, Input, Pagination, Row, Select, Skeleton, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getPluginCategories, getPlugins } from '../services/site'
import type { PluginListResponse } from '../types/site'
import { shortDate } from '../utils/format'

export function PluginsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<PluginListResponse | null>(null)
  const [categories, setCategories] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const searchKey = searchParams.toString()

  const query = {
    keyword: searchParams.get('q') || '',
    category: searchParams.get('category') || '',
    author: searchParams.get('author') || '',
    sort: searchParams.get('sort') || 'updated',
    page: Number(searchParams.get('page') || 1),
    pageSize: Number(searchParams.get('pageSize') || 12),
  }

  useEffect(() => {
    let mounted = true
    const nextParams = new URLSearchParams(searchKey)
    const requestQuery = {
      keyword: nextParams.get('q') || '',
      category: nextParams.get('category') || '',
      author: nextParams.get('author') || '',
      sort: nextParams.get('sort') || 'updated',
      page: Number(nextParams.get('page') || 1),
      pageSize: Number(nextParams.get('pageSize') || 12),
    }
    Promise.all([getPlugins(requestQuery), getPluginCategories()])
      .then(([plugins, nextCategories]) => {
        if (!mounted) return
        setData(plugins)
        setCategories(nextCategories)
      })
      .catch((err) => {
        if (mounted) setError(err instanceof Error ? err.message : '插件市场加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
    }
  }, [searchKey])

  const applyQuery = (values: typeof query) => {
    const next = new URLSearchParams()
    if (values.keyword) next.set('q', values.keyword)
    if (values.category) next.set('category', values.category)
    if (values.author) next.set('author', values.author)
    if (values.sort && values.sort !== 'updated') next.set('sort', values.sort)
    if (values.pageSize && values.pageSize !== 12) next.set('pageSize', String(values.pageSize))
    setSearchParams(next)
  }

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Tag icon={<AppstoreOutlined />} color="blue">插件市场</Tag>
        <Typography.Title level={2}>插件市场</Typography.Title>
        <Typography.Paragraph type="secondary">浏览、搜索、下载插件扩展。</Typography.Paragraph>
      </Card>
      <Card>
        <Form layout="inline" initialValues={query} onFinish={applyQuery}>
          <Form.Item name="keyword">
            <Input prefix={<SearchOutlined />} placeholder="名称、ID、描述" allowClear />
          </Form.Item>
          <Form.Item name="category">
            <Select
              allowClear
              placeholder="分类"
              style={{ width: 150 }}
              options={categories.map((category) => ({ label: category, value: category }))}
            />
          </Form.Item>
          <Form.Item name="author">
            <Input placeholder="作者" allowClear />
          </Form.Item>
          <Form.Item name="sort">
            <Select
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
                      <Button href={`/plugins/${plugin.pluginId}`}>详情</Button>
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
          current={data.page}
          pageSize={data.pageSize}
          total={data.totalCount}
          showSizeChanger
          pageSizeOptions={[12, 24, 48]}
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
