import { BookOutlined, CloudDownloadOutlined, FileMarkdownOutlined, FilterOutlined, MobileOutlined } from '@ant-design/icons'
import { Alert, Button, Card, Col, Collapse, Form, Row, Select, Skeleton, Space, Statistic, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { getReleases } from '../services/site'
import type { ReleasesPayload } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

const { Text } = Typography

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
    const nextParams = new URLSearchParams(searchKey)
    const requestParams = {
      major_minor: nextParams.get('major_minor') || '',
      branch: nextParams.get('branch') || '',
      kind: nextParams.get('kind') || '',
      era: nextParams.get('era') || '',
    }
    getReleases(requestParams)
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
    }
  }, [searchKey])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null
  const androidReleases = data.app_info.android_releases || []

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Row gutter={[16, 16]} align="middle">
          <Col flex="auto">
            <Tag color="blue">版本中心</Tag>
            <Typography.Title level={2}>版本档案</Typography.Title>
            <Typography.Paragraph type="secondary">当前根目录安装包与 History 归档制品。</Typography.Paragraph>
          </Col>
          <Col>
            <Space wrap>
              <Statistic title="当前版本" value={data.app_info.current_count || 0} />
              <Statistic title="Android" value={data.app_info.android_count || 0} />
              <Statistic title="历史制品" value={data.app_info.archive_count || 0} />
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

      {androidReleases.length > 0 && (
        <Card title={<Space><MobileOutlined />Android 安装包</Space>}>
          <Space direction="vertical" className="wide-space">
            {androidReleases.map((release) => (
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
          </Space>
        </Card>
      )}

      <Card title={<Space><FilterOutlined />历史筛选</Space>}>
        <Form
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

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={10}>
          <Card title="根目录保留版本">
            <Space direction="vertical" className="wide-space">
              {(data.app_info.current_releases || []).map((release) => (
                <div className="resource-row" key={release.relative_path}>
                  <div>
                    <Text strong>{release.display_title}</Text>
                    <div className="muted-line">
                      {release.filename} · {release.kind_label} · {humanSize(release.size)} · {shortDate(release.modified_display || release.modified)}
                    </div>
                  </div>
                  <Button type="primary" icon={<CloudDownloadOutlined />} href={downloadPath(release.relative_path)}>
                    下载
                  </Button>
                </div>
              ))}
              {(data.app_info.current_releases || []).length === 0 && <Text type="secondary">未检测到根目录版本文件。</Text>}
            </Space>
          </Card>
        </Col>
        <Col xs={24} xl={14}>
          <Card title={`归档历史时间线 · ${data.archive_visible_item_count} 条`}>
            <Collapse
              defaultActiveKey={data.archive_visible_groups.filter((g) => g.is_expanded).map((g) => g.branch || '')}
              items={data.archive_visible_groups.map((group, index) => ({
                key: group.branch || String(index),
                label: (
                  <Space wrap>
                    <Text strong>历史阶段 {group.branch}</Text>
                    <Tag>{group.visible_count || group.count} 条</Tag>
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
            <div className="card-footer-link">
              <Link to="/browse/History">打开 History 目录</Link>
            </div>
          </Card>
        </Col>
      </Row>
    </Space>
  )
}
