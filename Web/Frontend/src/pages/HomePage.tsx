import {
  AppstoreOutlined,
  CloudDownloadOutlined,
  FolderOpenOutlined,
  ReloadOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Alert, Button, Card, Col, Row, Skeleton, Space, Statistic, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { getHome } from '../services/site'
import type { HomePayload } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

const { Paragraph, Title, Text } = Typography

export function HomePage() {
  const [data, setData] = useState<HomePayload | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let mounted = true
    getHome()
      .then((payload) => {
        if (mounted) setData(payload)
      })
      .catch((err) => {
        if (mounted) setError(err instanceof Error ? err.message : '首页数据加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
    }
  }, [])

  if (loading) return <Skeleton active paragraph={{ rows: 8 }} />
  if (error) return <Alert type="error" message={error} />
  if (!data) return null

  const latest = data.app_info.current_preview?.[0]

  return (
    <Space direction="vertical" size={18} className="page-stack">
      <section className="site-hero">
        <div className="site-hero-main">
          <Tag color="blue">官方发布门户</Tag>
          <Title level={1}>ColorVision 发布中心</Title>
          <Paragraph>
            面向内部用户的统一下载与发布信息入口。安装包、插件、增量更新、工具软件和历史归档都在这里集中呈现。
          </Paragraph>
          <Space wrap>
            <Button type="primary" size="large" icon={<CloudDownloadOutlined />} href={downloadPath(latest?.relative_path)}>
              下载最新版本
            </Button>
            <Button size="large" icon={<AppstoreOutlined />} href="/plugins">
              插件市场
            </Button>
            <Button size="large" icon={<ReloadOutlined />} href="/updates">
              增量更新
            </Button>
          </Space>
        </div>
        <div className="site-hero-panel">
          <Row gutter={[12, 12]}>
            <Col span={12}>
              <Statistic title="最新版本" value={data.app_info.latest_version || '未检测'} />
            </Col>
            <Col span={12}>
              <Statistic title="当前安装包" value={data.app_info.current_count || 0} />
            </Col>
            <Col span={12}>
              <Statistic title="历史制品" value={data.app_info.archive_count || 0} />
            </Col>
            <Col span={12}>
              <Statistic title="增量包" value={data.update_summary.canonical_count || 0} />
            </Col>
          </Row>
        </div>
      </section>

      <Row gutter={[16, 16]}>
        {[
          { title: '版本中心', desc: '当前安装包与 History 归档制品。', href: '/releases', icon: <CloudDownloadOutlined /> },
          { title: '插件市场', desc: '插件扩展、版本详情和包下载。', href: '/plugins', icon: <AppstoreOutlined /> },
          { title: '工具下载', desc: '驱动、服务包和内部工具软件。', href: '/tools', icon: <ToolOutlined /> },
        ].map((item) => (
          <Col xs={24} md={8} key={item.href}>
            <Link to={item.href} className="command-link">
              <Card hoverable className="command-card">
                <Space align="start">
                  <span className="command-icon">{item.icon}</span>
                  <span>
                    <Text strong>{item.title}</Text>
                    <Paragraph type="secondary" className="compact-paragraph">
                      {item.desc}
                    </Paragraph>
                  </span>
                </Space>
              </Card>
            </Link>
          </Col>
        ))}
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={13}>
          <Card title="推荐下载" extra={<Link to="/releases">全部版本</Link>}>
            <Space direction="vertical" className="wide-space">
              {(data.app_info.current_preview || []).slice(0, 4).map((release) => (
                <div className="resource-row" key={release.relative_path}>
                  <div>
                    <Text strong>{release.display_title || release.version}</Text>
                    <div className="muted-line">
                      {release.filename} · {release.kind_label} · {humanSize(release.size)}
                    </div>
                  </div>
                  <Button href={downloadPath(release.relative_path)}>下载</Button>
                </div>
              ))}
              {(data.app_info.current_preview || []).length === 0 && <Text type="secondary">暂无标准安装包。</Text>}
            </Space>
          </Card>
        </Col>
        <Col xs={24} xl={11}>
          <Card title="发布动态">
            <Space direction="vertical" className="wide-space">
              {data.recent_change_dashboard.slice(0, 6).map((item) => (
                <div className="resource-row" key={`${item.title}-${item.href}`}>
                  <div>
                    <Text strong>{item.title}</Text>
                    <div className="muted-line">
                      {item.category} · {item.subtitle} · {shortDate(item.timestamp)}
                    </div>
                  </div>
                  <Button href={item.href}>{item.action_label}</Button>
                </div>
              ))}
            </Space>
          </Card>
        </Col>
      </Row>

      <Card title="服务与资料" extra={<Link to="/browse">文件浏览</Link>}>
        <Row gutter={[12, 12]}>
          {data.filesystem_spotlight.map((item) => (
            <Col xs={24} md={12} xl={8} key={item.name}>
              <Link to={item.href} className="command-link">
                <div className="directory-tile">
                  <FolderOpenOutlined />
                  <div>
                    <Text strong>{item.label}</Text>
                    <div className="muted-line">
                      {item.exists ? `${item.file_count} 个文件 · ${shortDate(item.modified)}` : '目录尚未创建'}
                    </div>
                  </div>
                </div>
              </Link>
            </Col>
          ))}
        </Row>
      </Card>
    </Space>
  )
}
