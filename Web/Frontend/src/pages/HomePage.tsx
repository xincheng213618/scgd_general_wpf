import {
  AppstoreOutlined,
  ArrowRightOutlined,
  CloudDownloadOutlined,
  DashboardOutlined,
  FileDoneOutlined,
  FileTextOutlined,
  FolderOpenOutlined,
  HistoryOutlined,
  ReloadOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Alert, Button, Col, Row, Skeleton, Space, Tag, Typography } from 'antd'
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

  const latest = data.app_info.current_preview?.[0] ?? data.app_info.latest_release
  const latestVersion = data.app_info.latest_version || latest?.version || '未检测'
  const currentCount = data.app_info.current_count ?? 0
  const archiveCount = data.app_info.archive_count ?? 0
  const updateCount = data.update_summary.canonical_count ?? 0
  const toolCount = (data.tool_summary.directory_count ?? 0) + (data.tool_summary.file_count ?? data.tool_items.length ?? 0)
  const overviewDirectories = data.overview_summary.directory_count ?? data.filesystem_spotlight.length
  const overviewFiles = data.overview_summary.total_file_count ?? data.overview_summary.file_count ?? 0

  const proofItems = [
    { label: '最新版本', value: latestVersion },
    { label: '当前制品', value: `${currentCount}` },
    { label: '历史制品', value: archiveCount.toLocaleString() },
    { label: '增量包', value: `${updateCount}` },
    { label: '工具项目', value: `${toolCount}` },
  ]

  const entryCards = [
    {
      title: '下载当前版本',
      desc: '面向普通用户，直接获取最新安装包。',
      href: downloadPath(latest?.relative_path),
      icon: <CloudDownloadOutlined />,
      meta: latest?.filename || latestVersion,
      primary: true,
    },
    {
      title: '版本中心',
      desc: '查看当前安装包、History 归档和筛选历史版本。',
      href: '/releases',
      icon: <FileDoneOutlined />,
      meta: `${currentCount} 当前制品`,
    },
    {
      title: '插件市场',
      desc: '插件扩展、说明文档和安装包下载。',
      href: '/plugins',
      icon: <AppstoreOutlined />,
      meta: '插件分发',
    },
    {
      title: '增量更新',
      desc: 'Update 目录增量包和保留策略。',
      href: '/updates',
      icon: <ReloadOutlined />,
      meta: `${updateCount} 个增量包`,
    },
    {
      title: '工具 / 软件',
      desc: '内部工具、服务包和驱动安装包。',
      href: '/tools',
      icon: <ToolOutlined />,
      meta: `${toolCount} 个项目`,
    },
    {
      title: '发布管理',
      desc: '进入后台处理上传、缓存、作业和 API Key。',
      href: '/admin',
      icon: <DashboardOutlined />,
      meta: '后台维护',
    },
  ]

  return (
    <div className="home-landing">
      <section className="codex-style-hero">
        <video className="hero-bg-video" src="/media/floral_a.mp4" autoPlay muted loop playsInline aria-hidden="true" />
        <div className="landing-hero-inner">
          <span className="home-product-icon">
            <img src="/brand/colorvision-icon.png" alt="ColorVision" />
          </span>
          <Title level={1}>ColorVision</Title>
          <Space wrap className="landing-hero-actions">
            <Button type="primary" size="large" shape="round" icon={<CloudDownloadOutlined />} href={downloadPath(latest?.relative_path)}>
              下载 Windows 版
            </Button>
            <Button size="large" shape="round" icon={<AppstoreOutlined />} href="/plugins">
              插件市场
            </Button>
          </Space>
          <div className="landing-version-note">
            当前发布版本 {latestVersion}
            {latest?.modified && <span> · {shortDate(latest.modified_display || latest.modified)}</span>}
          </div>
        </div>
        <div className="landing-proof" aria-label="发布概况">
          {proofItems.map((item) => (
            <span key={item.label}>
              <strong>{item.value}</strong>
              {item.label}
            </span>
          ))}
        </div>
      </section>

      <section className="landing-workflows">
        <div className="landing-section-heading">
          <Title level={3}>使用发布中心的方式</Title>
          <Paragraph>下载、扩展、维护和核对目录都从这里开始；首页保留最短路径，复杂操作进入后台。</Paragraph>
        </div>
        <Row gutter={[14, 14]}>
          {entryCards.map((item) => (
            <Col xs={24} md={12} xl={8} key={item.title}>
              <a href={item.href} className={`landing-workflow-card ${item.primary ? 'primary' : ''}`}>
                <span className="workflow-icon">{item.icon}</span>
                <span className="workflow-copy">
                  <Text strong>{item.title}</Text>
                  <span>{item.desc}</span>
                </span>
                <span className="workflow-foot">
                  {item.meta}
                  <ArrowRightOutlined />
                </span>
              </a>
            </Col>
          ))}
        </Row>
      </section>

      <section className="portal-section home-section">
        <div className="section-heading">
          <div>
            <span className="section-kicker">
              <FolderOpenOutlined />
              首页文件系统视角
            </span>
            <Title level={3}>核心目录</Title>
            <Paragraph>把最重要的目录放到首页上层，发布页用于阅读，文件系统页用于核对真实历史。</Paragraph>
          </div>
          <Space wrap>
            <span className="metric-chip">{overviewDirectories} 目录</span>
            <span className="metric-chip">{overviewFiles} 文件</span>
            <Button size="small" icon={<FolderOpenOutlined />} href="/browse">
              文件浏览
            </Button>
          </Space>
        </div>
        <Row gutter={[10, 10]}>
          {data.filesystem_spotlight.map((item) => (
            <Col xs={24} md={12} xl={8} key={item.name}>
              <Link to={item.href} className="directory-card">
                <span className="directory-icon">
                  <FolderOpenOutlined />
                </span>
                <span className="directory-body">
                  <Text strong>{item.label}</Text>
                  <span>{item.description}</span>
                  <small>{item.exists ? `最近更新 ${shortDate(item.modified)}` : '目录尚未创建'}</small>
                </span>
                <span className="directory-count">{item.exists ? `${item.file_count} 个文件` : '未创建'}</span>
              </Link>
            </Col>
          ))}
        </Row>
      </section>

      <Row gutter={[16, 16]} className="portal-main-grid home-section">
        <Col xs={24} xl={13}>
          <section className="portal-panel">
            <div className="section-heading compact">
              <div>
                <span className="section-kicker">
                  <CloudDownloadOutlined />
                  推荐下载
                </span>
                <Paragraph>把用户最可能需要的版本放在前面，完整历史交给版本中心。</Paragraph>
              </div>
              <Button size="small" href="/releases">
                全部版本
              </Button>
            </div>
            <div className="resource-list">
              {(data.app_info.current_preview || []).slice(0, 5).map((release) => (
                <div className="resource-row" key={release.relative_path}>
                  <span className="resource-icon">
                    <FileDoneOutlined />
                  </span>
                  <div className="resource-main">
                    <Text strong>{release.display_title || release.version}</Text>
                    <div className="muted-line">
                      {release.filename} · {release.kind_label} · {humanSize(release.size)}
                    </div>
                  </div>
                  <Button type="primary" ghost href={downloadPath(release.relative_path)}>
                    下载
                  </Button>
                </div>
              ))}
              {(data.app_info.current_preview || []).length === 0 && <Text type="secondary">暂无标准安装包。</Text>}
            </div>
          </section>
        </Col>
        <Col xs={24} xl={11}>
          <section className="portal-panel">
            <div className="section-heading compact">
              <div>
                <span className="section-kicker">
                  <ReloadOutlined />
                  发布动态
                </span>
                <Paragraph>最近变更只保留摘要，避免首页被维护信息淹没。</Paragraph>
              </div>
              <Tag color="blue">{data.recent_change_dashboard.length} 条</Tag>
            </div>
            <div className="resource-list">
              {data.recent_change_dashboard.slice(0, 6).map((item) => (
                <div className="resource-row" key={`${item.title}-${item.href}`}>
                  <span className="resource-icon subtle">
                    <FileTextOutlined />
                  </span>
                  <div className="resource-main">
                    <Text strong>{item.title}</Text>
                    <div className="muted-line">
                      {item.category} · {item.subtitle} · {shortDate(item.timestamp)}
                    </div>
                  </div>
                  <Button href={item.href}>{item.action_label}</Button>
                </div>
              ))}
            </div>
          </section>
        </Col>
      </Row>

      <section className="home-history-link">
        <Link to="/browse/History">
          <HistoryOutlined />
          History 文件系统
          <span>{archiveCount.toLocaleString()} 个历史制品</span>
        </Link>
      </section>
    </div>
  )
}
