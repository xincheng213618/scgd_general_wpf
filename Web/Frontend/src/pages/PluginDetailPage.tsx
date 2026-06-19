import { AppstoreOutlined, CloudDownloadOutlined } from '@ant-design/icons'
import { Alert, Avatar, Button, Card, Descriptions, Skeleton, Space, Table, Tabs, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getPluginDetail } from '../services/site'
import type { PluginDetail, PluginVersion } from '../types/site'
import { humanSize, shortDate } from '../utils/format'

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
        <Button icon={<CloudDownloadOutlined />} href={`/api/packages/${pluginId}/${record.version}`}>
          下载
        </Button>
      ),
    },
  ]
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

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Space align="start">
          <Avatar shape="square" size={64} src={plugin.iconUrl || undefined} icon={<AppstoreOutlined />} />
          <div>
            <Typography.Title level={2}>{plugin.name}</Typography.Title>
            <Typography.Paragraph type="secondary">{plugin.description || plugin.pluginId}</Typography.Paragraph>
            <Space wrap>
              <Tag color="blue">v{plugin.latestVersion || '-'}</Tag>
              {plugin.category && <Tag>{plugin.category}</Tag>}
              {plugin.author && <Tag>{plugin.author}</Tag>}
              <Tag>下载 {plugin.totalDownloads || 0}</Tag>
              <Link to="/plugins">返回插件市场</Link>
            </Space>
          </div>
        </Space>
      </Card>
      <Card>
        <Descriptions column={{ xs: 1, md: 2 }} bordered size="small">
          <Descriptions.Item label="插件 ID">{plugin.pluginId}</Descriptions.Item>
          <Descriptions.Item label="最低版本">{plugin.requiresVersion || '-'}</Descriptions.Item>
          <Descriptions.Item label="当前包">{plugin.currentPackageCount || 0}</Descriptions.Item>
          <Descriptions.Item label="历史包">{plugin.historicalPackageCount || 0}</Descriptions.Item>
          <Descriptions.Item label="更新时间">{shortDate(plugin.updatedAt)}</Descriptions.Item>
          <Descriptions.Item label="主页">{plugin.url ? <a href={plugin.url}>{plugin.url}</a> : '-'}</Descriptions.Item>
        </Descriptions>
      </Card>
      <Tabs
        items={[
          {
            key: 'versions',
            label: '版本下载',
            children: <Table rowKey={(row) => `${row.source}-${row.version}`} columns={versionColumns(plugin.pluginId)} dataSource={versions} />,
          },
          {
            key: 'readme',
            label: 'README',
            children: <pre className="text-pre">{plugin.readme || '暂无 README'}</pre>,
          },
          {
            key: 'changelog',
            label: '更新日志',
            children: <pre className="text-pre">{plugin.changelog || '暂无更新日志'}</pre>,
          },
        ]}
      />
    </Space>
  )
}
