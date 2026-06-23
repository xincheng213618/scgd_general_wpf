import { FileMarkdownOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Skeleton, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getChangelog } from '../services/site'

export function ChangelogPage() {
  const [data, setData] = useState<{ latest_version?: string; changelog_html?: string } | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    getChangelog()
      .then((payload) => setData(payload.app_info))
      .catch((err) => setError(err instanceof Error ? err.message : '更新说明加载失败'))
      .finally(() => setLoading(false))
  }, [])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Tag icon={<FileMarkdownOutlined />} color="blue">CHANGELOG</Tag>
        <Typography.Title level={2}>更新说明</Typography.Title>
        <Space wrap>
          <Tag>当前版本 {data.latest_version || '未检测'}</Tag>
          <Button href="/download/CHANGELOG.md">下载原始文件</Button>
          <Link to="/browse/CHANGELOG.md">文件浏览器</Link>
        </Space>
      </Card>
      <Card title="变更记录">
        {data.changelog_html ? (
          <div className="markdown-body" dangerouslySetInnerHTML={{ __html: data.changelog_html }} />
        ) : (
          <Typography.Text type="secondary">未检测到 CHANGELOG.md</Typography.Text>
        )}
      </Card>
    </Space>
  )
}
