import {
  AppstoreOutlined,
  ArrowRightOutlined,
  BookOutlined,
  CloudDownloadOutlined,
  FileDoneOutlined,
  FileTextOutlined,
  InboxOutlined,
  MobileOutlined,
  ReloadOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Alert, Button, Col, Row, Skeleton, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { getHome } from '../services/site'
import type { HomePayload } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

const { Title, Text } = Typography

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
  const latestAndroid = data.app_info.latest_android_release
  const latestVersion = data.app_info.latest_version || latest?.version || '未检测'
  const currentCount = data.app_info.current_count ?? 0
  const androidCount = data.app_info.android_count ?? 0
  const updateCount = data.update_summary.canonical_count ?? 0
  const toolCount = (data.tool_summary.directory_count ?? 0) + (data.tool_summary.file_count ?? data.tool_items.length ?? 0)
  const entryCount = 6
  const releaseItems = (data.app_info.current_preview || []).slice(0, 3)
  const recentItems = data.recent_change_dashboard.slice(0, 3)
  const docItems = ((data.docs?.featured?.length ? data.docs.featured : data.docs?.recent) || []).slice(0, 4)

  const proofItems = [
    { label: '最新版本', value: latestVersion },
    { label: '桌面端', value: `${currentCount}` },
    ...(latestAndroid ? [{ label: 'Android APK', value: `${androidCount}` }] : []),
    { label: '增量包', value: `${updateCount}` },
    { label: '工具项目', value: `${toolCount}` },
    { label: '常用入口', value: `${entryCount}` },
  ]

  const platformDownloads = [
    {
      key: 'windows',
      className: 'windows',
      eyebrow: 'Windows 桌面端',
      title: `ColorVision ${latestVersion}`,
      desc: '完整桌面端软件，适合工作站、检测电脑和正式生产环境。',
      meta: `${latest?.filename || 'ColorVision 安装包'}${latest?.size ? ` · ${humanSize(latest.size)}` : ''}`,
      action: '下载 Windows 版',
      href: downloadPath(latest?.relative_path),
      icon: <FileDoneOutlined />,
      available: Boolean(latest?.relative_path),
    },
    {
      key: 'android',
      className: 'android',
      eyebrow: 'Android APK',
      title: latestAndroid?.version ? `ColorVision Android ${latestAndroid.version}` : 'ColorVision Android',
      desc: '手机端安装包，用于扫码连接局域网控制、打开移动端页面。',
      meta: latestAndroid
        ? `${latestAndroid.filename || 'Android APK'}${latestAndroid.size ? ` · ${humanSize(latestAndroid.size)}` : ''}`
        : '暂无可下载 APK',
      action: '下载 Android APK',
      href: downloadPath(latestAndroid?.relative_path),
      icon: <MobileOutlined />,
      available: Boolean(latestAndroid?.relative_path),
    },
  ]

  const featureCards = [
    {
      title: '版本中心',
      desc: '桌面端、Android 与版本详情。',
      href: '/releases',
      icon: <FileDoneOutlined />,
      meta: `${currentCount} 个桌面端制品`,
    },
    {
      title: '插件市场',
      desc: '插件扩展和文档下载。',
      href: '/plugins',
      icon: <AppstoreOutlined />,
      meta: '插件分发',
    },
    {
      title: '增量更新',
      desc: '快速获取更新包。',
      href: '/updates',
      icon: <ReloadOutlined />,
      meta: `${updateCount} 个增量包`,
    },
    {
      title: '工具 / 软件',
      desc: '驱动、服务包和常用工具。',
      href: '/tools',
      icon: <ToolOutlined />,
      meta: `${toolCount} 个项目`,
    },
    {
      title: '文档中心',
      desc: '正式文档、开发指南和 API 参考。',
      href: '/scgd_general_wpf/',
      icon: <BookOutlined />,
      meta: 'VitePress 文档站',
    },
    {
      title: '文件中转',
      desc: '登录后上传与下载文件。',
      href: '/transfer',
      icon: <InboxOutlined />,
      meta: '登录可用',
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
          {latestAndroid && (
            <div className="landing-mobile-note">
              Android 版 {latestAndroid.version} 可用，手机端建议优先安装移动版。
            </div>
          )}
          <Space wrap className="landing-hero-actions">
            {latestAndroid && (
              <Button className="hero-action-android" size="large" shape="round" icon={<MobileOutlined />} href={downloadPath(latestAndroid.relative_path)}>
                下载 Android APK
              </Button>
            )}
            <Button className="hero-action-windows" type="primary" size="large" shape="round" icon={<CloudDownloadOutlined />} href={downloadPath(latest?.relative_path)}>
              下载 Windows 桌面端
            </Button>
            <Button className="hero-action-plugins" size="large" shape="round" icon={<AppstoreOutlined />} href="/plugins">
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

      <section className="home-platform-downloads" aria-label="ColorVision 下载">
        {platformDownloads.map((item) => {
          const body = (
            <>
              <span className="platform-card-top">
                <span>
                  <span className="platform-card-eyebrow">{item.eyebrow}</span>
                  <span className="platform-card-title">{item.title}</span>
                </span>
                <span className="platform-card-icon">{item.icon}</span>
              </span>
              <span className="platform-card-desc">{item.desc}</span>
              <span className="platform-card-meta">{item.meta}</span>
              <span className="platform-card-action">
                {item.action}
                <CloudDownloadOutlined />
              </span>
            </>
          )
          const className = `home-platform-card ${item.className}${item.available ? '' : ' disabled'}`
          return item.available ? (
            <a href={item.href} className={className} key={item.key}>
              {body}
            </a>
          ) : (
            <div className={className} key={item.key}>
              {body}
            </div>
          )
        })}
      </section>

      <section className="landing-workflows home-resource-hub">
        <div className="home-section-label">精选入口</div>
        <Row gutter={[14, 14]}>
          {featureCards.map((item) => (
            <Col xs={24} md={12} xl={6} key={item.title}>
              <a href={item.href} className="landing-workflow-card">
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

      <section className="home-split-showcase">
        <div>
          <section className="home-curated-panel">
            <div className="home-panel-heading">
              <div>
                <span className="home-panel-kicker">
                  <CloudDownloadOutlined />
                  精选
                </span>
                <Title level={3}>桌面端版本</Title>
              </div>
              <Button href="/releases">版本中心</Button>
            </div>
            <div className="home-release-list">
              {releaseItems.map((release) => (
                <a href={downloadPath(release.relative_path)} className="home-release-item" key={release.relative_path}>
                  <span className="home-release-icon">
                    {release.platform === 'android' ? <MobileOutlined /> : <FileDoneOutlined />}
                  </span>
                  <span className="home-release-copy">
                    <strong>{release.display_title || release.version}</strong>
                    <small>{release.platform_label || release.kind_label} · {release.kind_label} · {humanSize(release.size)}</small>
                  </span>
                  <span className="home-release-action">下载</span>
                </a>
              ))}
              {releaseItems.length === 0 && <Text type="secondary">暂无桌面端安装包。</Text>}
            </div>
          </section>
        </div>
        <div>
          <section className="home-curated-panel compact">
            <div className="home-panel-heading">
              <div>
                <span className="home-panel-kicker">
                  <ReloadOutlined />
                  新近
                </span>
                <Title level={3}>最新内容</Title>
              </div>
              <Tag color="blue">{recentItems.length}</Tag>
            </div>
            <div className="home-news-list">
              {recentItems.map((item) => (
                <a href={item.href} className="home-news-item" key={`${item.title}-${item.href}`}>
                  <span>
                    <FileTextOutlined />
                  </span>
                  <strong>{item.title}</strong>
                  <small>{shortDate(item.timestamp)}</small>
                </a>
              ))}
            </div>
          </section>
        </div>
      </section>

      {docItems.length > 0 && (
        <section className="home-curated-panel home-docs-panel">
          <div className="home-panel-heading">
            <div>
              <span className="home-panel-kicker">
                <BookOutlined />
                文档
              </span>
              <Title level={3}>常用资料</Title>
            </div>
            <Button href={data.docs?.entryUrl || '/scgd_general_wpf/'}>文档中心</Button>
          </div>
          <div className="home-doc-grid">
            {docItems.map((item) => (
              <a href={item.href} className="home-doc-card" key={item.path}>
                <span>{item.categoryLabel || '文档'}</span>
                <strong>{item.title}</strong>
                {item.excerpt && <small>{item.excerpt}</small>}
                <em>
                  打开
                  <ArrowRightOutlined />
                </em>
              </a>
            ))}
          </div>
        </section>
      )}
    </div>
  )
}
