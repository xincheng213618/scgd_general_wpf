import {
  AppstoreOutlined,
  ArrowRightOutlined,
  BookOutlined,
  CloudDownloadOutlined,
  FileMarkdownOutlined,
  FileTextOutlined,
  HistoryOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons'
import { Alert, Avatar, Button, Descriptions, Pagination, Skeleton, Space, Table, Tabs, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { getPluginDetail } from '../services/site'
import type { PluginDetail, PluginVersion } from '../types/site'
import { humanSize, shortDate } from '../utils/format'
import { sanitizeHtml } from '../utils/sanitizeHtml'

function MarkdownPanel({
  html,
  title,
  description,
}: {
  html?: string
  title: string
  description: string
}) {
  if (!html) {
    return (
      <div className="plugin-empty-state">
        <span className="plugin-empty-icon">
          <FileMarkdownOutlined />
        </span>
        <div>
          <Typography.Title level={4}>{title}</Typography.Title>
          <Typography.Paragraph type="secondary">{description}</Typography.Paragraph>
          <Space wrap>
            <Button href="/scgd_general_wpf/02-developer-guide/plugin-development/overview">
              插件文档
            </Button>
            <Button href="/admin/publish">发布管理</Button>
          </Space>
        </div>
      </div>
    )
  }
  return <article className="markdown-body plugin-markdown-body" dangerouslySetInnerHTML={{ __html: sanitizeHtml(html) }} />
}

function versionColumns(pluginId: string): ColumnsType<PluginVersion> {
  return [
    { title: '版本', dataIndex: 'version', render: (value) => <Typography.Text strong>v{value}</Typography.Text> },
    { title: '来源', dataIndex: 'source', width: 120, render: (value) => <Tag>{value === 'archive' ? 'History' : '当前'}</Tag> },
    { title: '大小', dataIndex: 'fileSize', width: 120, render: (value) => humanSize(value) },
    { title: '时间', dataIndex: 'createdAt', width: 180, render: (value) => shortDate(value) },
    {
      title: '操作',
      width: 120,
      render: (_, record) => (
        <Button icon={<CloudDownloadOutlined />} href={`/api/packages/${pluginId}/${record.version}`} shape="round">
          下载
        </Button>
      ),
    },
  ]
}

const archivePageSize = 20

function positivePage(value: string | null) {
  const parsed = Number(value)
  return Number.isInteger(parsed) && parsed > 0 ? parsed : 1
}

function VersionTable({ pluginId, versions, loading = false }: { pluginId: string; versions: PluginVersion[]; loading?: boolean }) {
  if (!versions.length) {
    return (
      <div className="plugin-empty-state compact">
        <span className="plugin-empty-icon">
          <CloudDownloadOutlined />
        </span>
        <div>
          <Typography.Title level={4}>还没有可下载版本</Typography.Title>
          <Typography.Paragraph type="secondary">
            这个插件已经建档，但还没有检测到当前包或 History 归档包。
          </Typography.Paragraph>
          <Button type="primary" href="/admin/publish">
            去发布
          </Button>
        </div>
      </div>
    )
  }

  return (
    <Table
      rowKey={(row) => `${row.source}-${row.version}`}
      columns={versionColumns(pluginId)}
      dataSource={versions}
      loading={loading}
      pagination={false}
      className="plugin-version-table"
      scroll={{ x: 680 }}
    />
  )
}

export function PluginDetailPage() {
  const { pluginId = '' } = useParams()
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const requestedArchivePage = positivePage(searchParams.get('archive_page'))
  const searchKey = searchParams.toString()
  const [plugin, setPlugin] = useState<PluginDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setError('')
      setPlugin((current) => current?.pluginId === pluginId ? current : null)
    })
    const nextParams = new URLSearchParams(searchKey)
    getPluginDetail(pluginId, {
      archivePage: positivePage(nextParams.get('archive_page')),
      archivePageSize,
    }, controller.signal)
      .then((payload) => {
        if (mounted) setPlugin(payload)
      })
      .catch((err) => {
        if (mounted) setError(err instanceof Error ? err.message : '插件详情加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
      controller.abort()
    }
  }, [pluginId, searchKey])

  if (loading && !plugin) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error && !plugin) return <Alert type="error" message={error} />
  if (!plugin) return null

  const currentVersions = plugin.versions || []
  const archivedVersions = plugin.archivedVersions || []
  const docs = plugin.relatedDocs || []
  const latestHref = plugin.latestVersion ? `/api/packages/${plugin.pluginId}/${plugin.latestVersion}` : undefined

  const updateArchivePage = (page: number) => {
    const next = new URLSearchParams(searchParams)
    if (page > 1) next.set('archive_page', String(page))
    else next.delete('archive_page')
    setSearchParams(next)
  }

  return (
    <Space direction="vertical" size={16} className="page-stack">
      {error && <Alert type="error" message={error} showIcon />}
      <section className="plugin-detail-hero">
        <div className="plugin-detail-main">
          <Avatar className="plugin-detail-avatar" shape="square" size={72} src={plugin.iconUrl || undefined} icon={<AppstoreOutlined />} />
          <div className="plugin-detail-copy">
            <Space wrap size={8}>
              <Tag color="blue">插件详情</Tag>
              {plugin.category && <Tag>{plugin.category}</Tag>}
              {plugin.author && <Tag>{plugin.author}</Tag>}
            </Space>
            <Typography.Title level={1}>{plugin.name}</Typography.Title>
            <Typography.Paragraph>{plugin.description || plugin.pluginId}</Typography.Paragraph>
            <Space wrap className="plugin-detail-actions">
              <Button type="primary" shape="round" icon={<CloudDownloadOutlined />} href={latestHref} disabled={!latestHref}>
                下载最新版
              </Button>
              <Button shape="round" onClick={() => navigate('/plugins')}>
                返回插件市场
              </Button>
            </Space>
          </div>
        </div>
        <div className="plugin-detail-metrics" aria-label="插件概况">
          <span>
            <strong>{plugin.latestVersion || '-'}</strong>
            最新版本
          </span>
          <span>
            <strong>{plugin.currentPackageCount || 0}</strong>
            当前包
          </span>
          <span>
            <strong>{plugin.historicalPackageCount || 0}</strong>
            历史包
          </span>
          <span>
            <strong>{plugin.totalDownloads || 0}</strong>
            下载
          </span>
        </div>
      </section>

      <div className="plugin-detail-grid">
        <section className="plugin-detail-panel plugin-doc-panel">
          <div className="plugin-panel-heading">
            <span>
              <BookOutlined />
              相关文档
            </span>
            <a href="/scgd_general_wpf/">
              文档中心
              <ArrowRightOutlined />
            </a>
          </div>
          <div className="plugin-doc-links">
            {docs.length > 0 ? (
              docs.map((doc) => (
                <a href={doc.href} className="plugin-doc-link" key={doc.href}>
                  <span>
                    <BookOutlined />
                  </span>
                  <strong>{doc.title}</strong>
                  {doc.description && <small>{doc.description}</small>}
                </a>
              ))
            ) : (
              <div className="plugin-doc-empty">
                <BookOutlined />
                <span>暂无关联文档</span>
              </div>
            )}
          </div>
        </section>

        <section className="plugin-detail-panel">
          <div className="plugin-panel-heading">
            <span>
              <InfoCircleOutlined />
              发布信息
            </span>
          </div>
          <Descriptions column={1} size="small" className="plugin-info-list">
            <Descriptions.Item label="插件 ID">{plugin.pluginId}</Descriptions.Item>
            <Descriptions.Item label="最低版本">{plugin.requiresVersion || '-'}</Descriptions.Item>
            <Descriptions.Item label="更新时间">{shortDate(plugin.updatedAt)}</Descriptions.Item>
            <Descriptions.Item label="主页">
              {plugin.url ? (
                <a href={plugin.url} target="_blank" rel="noreferrer">
                  {plugin.url}
                </a>
              ) : (
                '-'
              )}
            </Descriptions.Item>
          </Descriptions>
        </section>
      </div>

      <section className="plugin-detail-panel plugin-tabs-panel">
        <Tabs
          items={[
            {
              key: 'versions',
              label: <Space><CloudDownloadOutlined />版本下载</Space>,
              children: (
                <Space direction="vertical" size={20} className="wide-space">
                  <div>
                    <Typography.Title level={4}>当前发布</Typography.Title>
                    <Typography.Paragraph type="secondary">插件目录中的当前可下载版本。</Typography.Paragraph>
                    <VersionTable pluginId={plugin.pluginId} versions={currentVersions} loading={loading} />
                  </div>
                  {(plugin.historicalPackageCount || 0) > 0 && (
                    <div>
                      <Typography.Title level={4}>History 历史版本</Typography.Title>
                      <Typography.Paragraph type="secondary">
                        当前页 {archivedVersions.length} 个，历史共 {plugin.historicalPackageCount} 个。
                      </Typography.Paragraph>
                      <VersionTable pluginId={plugin.pluginId} versions={archivedVersions} loading={loading} />
                      {plugin.archivedTotalPages > 1 && (
                        <div className="table-pager">
                          <Pagination
                            current={plugin.archivedPage || requestedArchivePage}
                            pageSize={plugin.archivedPageSize || archivePageSize}
                            total={plugin.historicalPackageCount || 0}
                            showSizeChanger={false}
                            disabled={loading}
                            showTotal={(total, range) => `${range[0]}-${range[1]} / ${total}`}
                            onChange={updateArchivePage}
                          />
                        </div>
                      )}
                    </div>
                  )}
                </Space>
              ),
            },
            {
              key: 'readme',
              label: <Space><FileTextOutlined />README</Space>,
              children: (
                <MarkdownPanel
                  html={plugin.readmeHtml}
                  title="README 还没有整理"
                  description="README 会直接展示给用户，建议补上安装方式、适用版本和注意事项。"
                />
              ),
            },
            {
              key: 'changelog',
              label: <Space><HistoryOutlined />更新日志</Space>,
              children: (
                <MarkdownPanel
                  html={plugin.changelogHtml}
                  title="暂无更新日志"
                  description="更新日志会帮助用户判断是否需要升级；没有内容时先保留简洁空态。"
                />
              ),
            },
          ]}
        />
      </section>
    </Space>
  )
}
