import { FileMarkdownOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Pagination, Skeleton, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getChangelog } from '../services/site'
import type { ChangelogPayload } from '../types/site'
import { sanitizeHtml } from '../utils/sanitizeHtml'

const pageSize = 20

function validPage(value: string | null) {
  const page = Number(value || 1)
  return Number.isInteger(page) && page > 0 ? page : 1
}

export function ChangelogPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<ChangelogPayload | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const page = validPage(searchParams.get('page'))

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setError('')
    })
    getChangelog({ page, page_size: pageSize }, controller.signal)
      .then((payload) => {
        if (mounted) setData(payload)
      })
      .catch((err) => {
        if (mounted) setError(err instanceof Error ? err.message : '更新说明加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
      controller.abort()
    }
  }, [page])

  if (loading && !data) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error && !data) return <Alert type="error" message={error} />
  if (!data) return null

  const appInfo = data.app_info

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Tag icon={<FileMarkdownOutlined />} color="blue">CHANGELOG</Tag>
        <Typography.Title level={2}>更新说明</Typography.Title>
        <Space wrap>
          <Tag>当前版本 {appInfo.latest_version || '未检测'}</Tag>
          <Tag>共 {data.changelog_total_entries} 个版本</Tag>
          <Button href="/download/CHANGELOG.md">下载原始文件</Button>
          <Link to="/browse/CHANGELOG.md">文件浏览器</Link>
        </Space>
      </Card>
      {error && <Alert type="warning" showIcon message="刷新失败，当前展示上一次成功结果" description={error} />}
      <Card title="变更记录" loading={loading}>
        {appInfo.changelog_html ? (
          <div className="markdown-body" dangerouslySetInnerHTML={{ __html: sanitizeHtml(appInfo.changelog_html) }} />
        ) : (
          <Typography.Text type="secondary">未检测到 CHANGELOG.md</Typography.Text>
        )}
        {data.changelog_total_pages > 1 && (
          <div className="table-pager">
            <Pagination
              current={data.changelog_page}
              pageSize={data.changelog_page_size}
              total={data.changelog_total_entries}
              showSizeChanger={false}
              showTotal={(total, range) => `${range[0]}-${range[1]} / ${total}`}
              onChange={(nextPage) => {
                const next = new URLSearchParams(searchParams)
                if (nextPage > 1) next.set('page', String(nextPage))
                else next.delete('page')
                setSearchParams(next)
              }}
            />
          </div>
        )}
      </Card>
    </Space>
  )
}
