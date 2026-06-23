import {
  AppstoreOutlined,
  BookOutlined,
  CloudDownloadOutlined,
  DatabaseOutlined,
  FolderOpenOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import { ProDescriptions } from '@ant-design/pro-components'
import { Alert, App, Badge, Button, Card, Col, List, Popconfirm, Row, Space, Statistic, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { cleanupCache, getAdminStats, getCacheStatus, getDocsStatus, refreshAllIndexes, refreshDocsIndex } from '../services/admin'
import type { AdminStats, CacheStatus, DocsStatus } from '../types/admin'

const { Text, Title } = Typography

export function Dashboard() {
  const { message } = App.useApp()
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [cache, setCache] = useState<CacheStatus | null>(null)
  const [docs, setDocs] = useState<DocsStatus | null>(null)
  const [loading, setLoading] = useState(true)

  const load = async () => {
    setLoading(true)
    try {
      const [nextStats, nextCache, nextDocs] = await Promise.all([getAdminStats(), getCacheStatus(), getDocsStatus()])
      setStats(nextStats)
      setCache(nextCache)
      setDocs(nextDocs)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '加载控制台失败')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let mounted = true
    Promise.all([getAdminStats(), getCacheStatus(), getDocsStatus()])
      .then(([nextStats, nextCache, nextDocs]) => {
        if (!mounted) return
        setStats(nextStats)
        setCache(nextCache)
        setDocs(nextDocs)
      })
      .catch((error) => {
        if (mounted) {
          message.error(error instanceof Error ? error.message : '加载控制台失败')
        }
      })
      .finally(() => {
        if (mounted) {
          setLoading(false)
        }
      })
    return () => {
      mounted = false
    }
  }, [message])

  const refreshIndexes = async () => {
    await refreshAllIndexes()
    message.success('索引刷新已完成')
    await load()
  }

  const refreshDocs = async () => {
    const result = await refreshDocsIndex()
    message.success(`文档索引已刷新：${result.indexed_count} 篇`)
    await load()
  }

  const cleanCache = async () => {
    const result = await cleanupCache()
    message.success(`已清理 ${result.deleted_count} 条过期缓存`)
    await load()
  }

  const docsHealth = docs?.healthStatus || (docs?.built ? 'ok' : 'warning')
  const docsAlertType = docsHealth === 'error' ? 'error' : docsHealth === 'warning' ? 'warning' : 'success'

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card loading={loading} className="hero-card">
        <Space direction="vertical" size={12}>
          <Tag color="blue">Web Admin</Tag>
          <Title level={2}>发布与运维控制台</Title>
          <Text type="secondary">
            管理安装包、插件、增量包、缓存索引、任务调度、API Key 与审计日志。
          </Text>
          <Space wrap>
            <Button type="primary" icon={<ReloadOutlined />} onClick={refreshIndexes}>
              刷新全部索引
            </Button>
            <Popconfirm title="确认清理过期缓存？" onConfirm={cleanCache}>
              <Button danger>清理过期缓存</Button>
            </Popconfirm>
            <Button href="/">前台发布站</Button>
            <Button icon={<BookOutlined />} href="/docs">
              文档中心
            </Button>
            <Button icon={<ReloadOutlined />} onClick={refreshDocs}>
              刷新文档索引
            </Button>
          </Space>
        </Space>
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="插件数量" value={stats?.pluginCount ?? 0} prefix={<AppstoreOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="包索引" value={stats?.packageCount ?? 0} prefix={<DatabaseOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="今日下载" value={stats?.downloadsToday ?? 0} prefix={<CloudDownloadOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="总下载" value={stats?.totalDownloads ?? 0} prefix={<CloudDownloadOutlined />} />
          </Card>
        </Col>
      </Row>

      <Card
        title="系统详情"
        loading={loading}
        extra={docs && <Tag color={docsHealth === 'ok' ? 'green' : docsHealth === 'error' ? 'red' : 'gold'}>{docsHealth === 'ok' ? '文档就绪' : docsHealth === 'error' ? '文档异常' : '文档待处理'}</Tag>}
      >
        {docs && docsHealth !== 'ok' && (
          <Alert
            type={docsAlertType}
            showIcon
            message={docs.healthMessage || '文档中心需要检查'}
            description={docs.actionHint || '刷新索引或重新构建文档站后再打开文档中心。'}
            className="admin-doc-alert"
            action={
              <Space wrap>
                <Button size="small" icon={<ReloadOutlined />} onClick={refreshDocs}>
                  刷新索引
                </Button>
                <Button size="small" icon={<BookOutlined />} href="/docs">
                  打开文档
                </Button>
              </Space>
            }
          />
        )}
        <ProDescriptions column={{ xs: 1, md: 2, xl: 3 }} dataSource={{ ...stats, ...cache }}>
          <ProDescriptions.Item label="最新版本" dataIndex="latestReleaseVersion">
            {stats?.latestReleaseVersion || '未检测到'}
          </ProDescriptions.Item>
          <ProDescriptions.Item label="插件目录">
            <Badge status={cache?.plugins_dir_exists ? 'success' : 'warning'} text={cache?.plugins_dir_exists ? '可用' : '待创建'} />
          </ProDescriptions.Item>
          <ProDescriptions.Item label="插件目录缓存">
            <Tag color={stats?.pluginCatalogCached ? 'green' : 'default'}>
              {stats?.pluginCatalogCached ? '已缓存' : '未缓存'}
            </Tag>
          </ProDescriptions.Item>
          <ProDescriptions.Item label="缓存条目">{cache?.cache_entry_count ?? 0}</ProDescriptions.Item>
          <ProDescriptions.Item label="过期缓存">{cache?.expired_cache_entry_count ?? 0}</ProDescriptions.Item>
          <ProDescriptions.Item label="文档站">
            <Badge status={docs?.built ? 'success' : 'warning'} text={docs?.built ? '已构建' : '待构建'} />
          </ProDescriptions.Item>
          <ProDescriptions.Item label="Markdown 文档">{docs?.sourceDocumentCount ?? 0}</ProDescriptions.Item>
          <ProDescriptions.Item label="文档索引">{docs?.indexedDocumentCount ?? 0}</ProDescriptions.Item>
          <ProDescriptions.Item label="构建页面">{docs?.builtPageCount ?? 0}</ProDescriptions.Item>
          <ProDescriptions.Item label="文档搜索">
            <Tag color={docs?.searchIndexExists ? 'green' : 'default'}>
              {docs?.searchIndexExists ? '可用' : '未生成'}
            </Tag>
          </ProDescriptions.Item>
          <ProDescriptions.Item label="最近构建">{docs?.lastBuildUpdate || '-'}</ProDescriptions.Item>
          <ProDescriptions.Item label="索引缓存">
            <Tag color={docs?.indexCached ? 'green' : 'blue'}>{docs?.indexCached ? '命中' : '已刷新'}</Tag>
          </ProDescriptions.Item>
          <ProDescriptions.Item label="索引更新时间">{docs?.indexUpdatedAt || '-'}</ProDescriptions.Item>
          <ProDescriptions.Item label="存储路径" span={3}>
            <div className="mono-line">{cache?.storage_path || '-'}</div>
          </ProDescriptions.Item>
          <ProDescriptions.Item label="文档路径" span={3}>
            <div className="mono-line">{docs?.sourcePath || '-'}</div>
          </ProDescriptions.Item>
          <ProDescriptions.Item label="构建命令" span={3}>
            <div className="mono-line">{docs?.buildCommand || 'npm run docs:build'}</div>
          </ProDescriptions.Item>
        </ProDescriptions>
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={12}>
          <Card title="文档分类" loading={loading}>
            <Space wrap>
              {Object.entries(docs?.categoryCounts || {}).map(([name, count]) => (
                <Tag color="blue" key={name}>{name} {count}</Tag>
              ))}
              {Object.keys(docs?.categoryCounts || {}).length === 0 && <Text type="secondary">暂无文档索引。</Text>}
            </Space>
          </Card>
        </Col>
        <Col xs={24} xl={12}>
          <Card title="最近更新文档" loading={loading}>
            <List
              size="small"
              dataSource={(docs?.recentDocuments || []).slice(0, 5)}
              locale={{ emptyText: '暂无文档索引' }}
              renderItem={(item) => (
                <List.Item actions={[<Button size="small" icon={<FolderOpenOutlined />} href={item.href} key="open">打开</Button>]}>
                  <List.Item.Meta
                    title={<a href={item.href}>{item.title}</a>}
                    description={`${item.categoryLabel} · ${item.localeLabel}${item.modified ? ` · ${item.modified}` : ''}`}
                  />
                </List.Item>
              )}
            />
          </Card>
        </Col>
      </Row>
    </Space>
  )
}
