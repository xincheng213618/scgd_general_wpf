import { BookOutlined, CloudDownloadOutlined, FileDoneOutlined, FileMarkdownOutlined, FilterOutlined, MobileOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Col, Collapse, Form, Pagination, Row, Select, Skeleton, Space, Statistic, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getReleases } from '../services/site'
import type { ReleasesPayload } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

const { Text } = Typography
const archivePageSize = 100
const androidArchivePageSize = 100

function archivePage(value: string | null) {
  const page = Number(value || 1)
  return Number.isInteger(page) && page > 0 ? page : 1
}

export function ReleasesPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<ReleasesPayload | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const searchKey = searchParams.toString()

  const params = {
    major_minor: searchParams.get('major_minor') || '',
    branch: searchParams.get('branch') || '',
    kind: searchParams.get('kind') || '',
    era: searchParams.get('era') || '',
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
    const requestParams = {
      major_minor: nextParams.get('major_minor') || '',
      branch: nextParams.get('branch') || '',
      kind: nextParams.get('kind') || '',
      era: nextParams.get('era') || '',
      page: archivePage(nextParams.get('page')),
      page_size: archivePageSize,
      android_page: archivePage(nextParams.get('android_page')),
      android_page_size: androidArchivePageSize,
    }
    getReleases(requestParams, controller.signal)
      .then((payload) => {
        if (mounted) setData(payload)
      })
      .catch((err) => {
        if (mounted) setError(err instanceof Error ? err.message : '版本数据加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
      controller.abort()
    }
  }, [searchKey])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null
  const androidReleases = data.app_info.android_releases || []
  const desktopReleases = data.app_info.current_releases || []
  const currentAndroidReleases = data.app_info.current_android_releases ?? androidReleases
  const archivedAndroidReleases = data.app_info.archived_android_releases || []

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Row gutter={[16, 16]} align="middle">
          <Col flex="auto">
            <Tag color="blue">版本中心</Tag>
            <Typography.Title level={2}>版本档案</Typography.Title>
            <Typography.Paragraph type="secondary">Windows 桌面端和 Android APK 分区下载，历史制品单独归档。</Typography.Paragraph>
          </Col>
          <Col>
            <Space wrap>
              <Statistic title="桌面端" value={data.app_info.current_count || 0} />
              <Statistic title="Android APK" value={data.app_info.android_count || 0} />
              <Statistic title="桌面历史" value={data.app_info.archive_count || 0} />
              <Statistic title="历史阶段" value={data.app_info.archive_timeline_count || 0} />
              <Button icon={<FileMarkdownOutlined />} href="/changelog">
                发布说明
              </Button>
              <Button icon={<BookOutlined />} href="/scgd_general_wpf/02-developer-guide/deployment/auto-update">
                自动更新文档
              </Button>
            </Space>
          </Col>
        </Row>
      </Card>

      <Row gutter={[16, 16]} className="release-platform-grid">
        <Col xs={24} lg={12}>
          <Card
            className="release-platform-card"
            title={<Space><FileDoneOutlined />Windows 桌面端</Space>}
            extra={<Tag>{desktopReleases.length} 个</Tag>}
          >
            <div className="release-platform-summary">
              <span className="release-platform-icon"><FileDoneOutlined /></span>
              <div className="release-platform-copy">
                <Typography.Title level={4}>桌面端安装包</Typography.Title>
                <Typography.Paragraph type="secondary">用于 Windows 工作站、检测电脑和正式生产环境。</Typography.Paragraph>
              </div>
            </div>
            <Space direction="vertical" className="wide-space release-platform-list">
              {desktopReleases.map((release) => (
                <div className="resource-row" key={release.relative_path}>
                  <div>
                    <Text strong>{release.display_title}</Text>
                    <div className="muted-line">
                      {release.filename} · {release.kind_label} · {humanSize(release.size)} · {shortDate(release.modified_display || release.modified)}
                    </div>
                  </div>
                  <Button type="primary" icon={<CloudDownloadOutlined />} href={downloadPath(release.relative_path)}>
                    下载桌面端
                  </Button>
                </div>
              ))}
              {desktopReleases.length === 0 && <Text type="secondary">未检测到桌面端版本文件。</Text>}
            </Space>
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card
            className="release-platform-card android"
            title={<Space><MobileOutlined />Android APK</Space>}
            extra={<Tag>{currentAndroidReleases.length} 个</Tag>}
          >
            <div className="release-platform-summary">
              <span className="release-platform-icon"><MobileOutlined /></span>
              <div className="release-platform-copy">
                <Typography.Title level={4}>移动端安装包</Typography.Title>
                <Typography.Paragraph type="secondary">用于手机扫码连接局域网控制页面，安装后直接进入移动端入口。</Typography.Paragraph>
              </div>
            </div>
            <Space direction="vertical" className="wide-space release-platform-list">
              {currentAndroidReleases.map((release) => (
                <div className="resource-row" key={release.relative_path}>
                  <div>
                    <Text strong>{release.display_title || `ColorVision Android ${release.version || ''}`}</Text>
                    <div className="muted-line">
                      {release.filename} · {release.kind_label} · {humanSize(release.size)} · {shortDate(release.modified_display || release.modified)}
                    </div>
                  </div>
                  <Button type="primary" icon={<CloudDownloadOutlined />} href={downloadPath(release.relative_path)}>
                    下载 APK
                  </Button>
                </div>
              ))}
              {currentAndroidReleases.length === 0 && <Text type="secondary">暂无 Android APK。</Text>}
            </Space>
          </Card>
        </Col>
      </Row>

      <Card title={<Space><FilterOutlined />Windows 桌面历史筛选</Space>}>
        <Form
          key={`${params.major_minor}:${params.branch}:${params.kind}:${params.era}`}
          layout="inline"
          initialValues={params}
          onFinish={(values) => {
            const next = new URLSearchParams()
            Object.entries(values).forEach(([key, value]) => {
              if (value) next.set(key, String(value))
            })
            setSearchParams(next)
          }}
        >
          <Form.Item name="major_minor" label="主线">
            <Select allowClear style={{ width: 150 }} options={data.archive_major_minor_options.map((i) => ({ label: i.label, value: i.value }))} />
          </Form.Item>
          <Form.Item name="branch" label="阶段">
            <Select allowClear style={{ width: 150 }} options={data.archive_branch_options.map((i) => ({ label: i.label, value: i.value }))} />
          </Form.Item>
          <Form.Item name="kind" label="类型">
            <Select allowClear style={{ width: 140 }} options={data.archive_kind_options.map((i) => ({ label: i.label, value: i.value }))} />
          </Form.Item>
          <Form.Item name="era" label="时代">
            <Select allowClear style={{ width: 160 }} options={data.archive_era_options.map((i) => ({ label: i.label, value: i.value }))} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit">筛选</Button>
              <Button onClick={() => setSearchParams({})}>清除</Button>
            </Space>
          </Form.Item>
        </Form>
      </Card>

      <Card title={`Windows 桌面端归档历史 · ${data.archive_visible_item_count} 条`}>
        <Collapse
          key={`${data.archive_page}:${params.major_minor}:${params.branch}:${params.kind}:${params.era}`}
          defaultActiveKey={data.archive_visible_groups.filter((g) => g.is_expanded).map((g) => g.branch || '')}
          items={data.archive_visible_groups.map((group, index) => ({
            key: group.branch || String(index),
            label: (
              <Space wrap>
                <Text strong>历史阶段 {group.branch}</Text>
                <Tag>{group.visible_count ?? group.count} 条</Tag>
                {group.contains_archive_only_formats && <Tag color="gold">ZIP / RAR</Tag>}
              </Space>
            ),
            children: (
              <Space direction="vertical" className="wide-space">
                <Text type="secondary">{group.time_range_display} · {group.visible_kind_summary || group.kind_summary}</Text>
                {(group.visible_items || []).map((release) => (
                  <div className="resource-row" key={release.relative_path}>
                    <div>
                      <Text strong>{release.display_title}</Text>
                      <div className="muted-line">
                        {release.era_label} · {release.kind_label} · {humanSize(release.size)} · {shortDate(release.modified_display || release.modified)}
                      </div>
                    </div>
                    <Button href={downloadPath(release.relative_path)}>下载</Button>
                  </div>
                ))}
              </Space>
            ),
          }))}
        />
        {data.archive_visible_groups.length === 0 && <Text type="secondary">暂无匹配的归档历史文件。</Text>}
        {data.archive_total_pages > 1 && (
          <div className="table-pager">
            <Pagination
              current={data.archive_page}
              pageSize={data.archive_page_size}
              total={data.archive_visible_item_count}
              showSizeChanger={false}
              showTotal={(total, range) => `${range[0]}-${range[1]} / ${total}`}
              onChange={(page) => {
                const next = new URLSearchParams(searchParams)
                if (page > 1) next.set('page', String(page))
                else next.delete('page')
                setSearchParams(next)
              }}
            />
          </div>
        )}
        <div className="card-footer-link">
          <Link to="/browse/History">打开 History 目录</Link>
        </div>
      </Card>

      {archivedAndroidReleases.length > 0 && (
        <Card title={<Space><MobileOutlined />Android APK 历史包 · {data.android_total_item_count} 条</Space>}>
          <Space direction="vertical" className="wide-space">
            {archivedAndroidReleases.map((release) => (
              <div className="resource-row" key={release.relative_path}>
                <div>
                  <Text strong>{release.display_title || `ColorVision Android ${release.version || ''}`}</Text>
                  <div className="muted-line">
                    {release.filename} · {release.kind_label} · {humanSize(release.size)} · {shortDate(release.modified_display || release.modified)}
                  </div>
                </div>
                <Button icon={<CloudDownloadOutlined />} href={downloadPath(release.relative_path)}>
                  下载 APK
                </Button>
              </div>
            ))}
          </Space>
          {data.android_total_pages > 1 && (
            <div className="table-pager">
              <Pagination
                current={data.android_page}
                pageSize={data.android_page_size}
                total={data.android_total_item_count}
                showSizeChanger={false}
                showTotal={(total, range) => `${range[0]}-${range[1]} / ${total}`}
                onChange={(page) => {
                  const next = new URLSearchParams(searchParams)
                  if (page > 1) next.set('android_page', String(page))
                  else next.delete('android_page')
                  setSearchParams(next)
                }}
              />
            </div>
          )}
        </Card>
      )}
    </Space>
  )
}
