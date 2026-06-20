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
import { Alert, Avatar, Button, Descriptions, Skeleton, Space, Table, Tabs, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { getPluginDetail } from '../services/site'
import type { PluginDetail, PluginVersion } from '../types/site'
import { humanSize, shortDate } from '../utils/format'

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
  return <article className="markdown-body plugin-markdown-body" dangerouslySetInnerHTML={{ __html: html }} />
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

function VersionTable({ pluginId, versions }: { pluginId: string; versions: PluginVersion[] }) {
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
      pagination={versions.length > 8 ? { pageSize: 8 } : false}
      className="plugin-version-table"
    />
  )
}

export function PluginDetailPage() {
  const { pluginId = '' } = useParams()
  const [plugin, setPlugin] = useState<PluginDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let mounted = true
    getPluginDetail(pluginId)
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
    }
  }, [pluginId])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!plugin) return null

  const versions = [...(plugin.versions || []), ...(plugin.archivedVersions || [])]
  const docs = plugin.relatedDocs || []
  const latestHref = plugin.latestVersion ? `/api/packages/${plugin.pluginId}/${plugin.latestVersion}` : undefined

  return (
    <Space direction="vertical" size={16} className="page-stack">
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
              <Button shape="round" href="/plugins">
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
              children: <VersionTable pluginId={plugin.pluginId} versions={versions} />,
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
