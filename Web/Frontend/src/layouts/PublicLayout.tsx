import {
  AppstoreOutlined,
  CloudDownloadOutlined,
  DashboardOutlined,
  FolderOpenOutlined,
  HomeOutlined,
  InboxOutlined,
  LoginOutlined,
  LogoutOutlined,
  MoonOutlined,
  ProductOutlined,
  SunOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Button, Layout, Menu, Segmented, Space, Typography } from 'antd'
import { useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { logout } from '../services/auth'
import type { ThemeMode } from '../types/admin'
import type { AuthSession } from '../types/site'

const { Header, Content } = Layout

const menuItems = [
  { key: '/', icon: <HomeOutlined />, label: '首页' },
  { key: '/plugins', icon: <AppstoreOutlined />, label: '插件市场' },
  { key: '/releases', icon: <CloudDownloadOutlined />, label: '版本中心' },
  { key: '/updates', icon: <ProductOutlined />, label: '增量更新' },
  { key: '/tools', icon: <ToolOutlined />, label: '工具下载' },
  { key: '/transfer', icon: <InboxOutlined />, label: '文件中转' },
  { key: '/browse', icon: <FolderOpenOutlined />, label: '文件浏览' },
]

function selectedKey(pathname: string) {
  const match = [...menuItems].reverse().find((item) => item.key !== '/' && pathname.startsWith(item.key))
  return match?.key ?? '/'
}

export function PublicLayout({
  children,
  mode,
  setMode,
  session,
  onSessionChanged,
}: {
  children: ReactNode
  mode: ThemeMode
  setMode: (mode: ThemeMode) => void
  session: AuthSession | null
  onSessionChanged: () => Promise<void>
}) {
  const location = useLocation()
  const navigate = useNavigate()
  const isHome = location.pathname === '/'
  const [homeScrolled, setHomeScrolled] = useState(false)

  useEffect(() => {
    if (!isHome) {
      return
    }
    const syncScrollState = () => setHomeScrolled(window.scrollY > 12)
    const frame = window.requestAnimationFrame(syncScrollState)
    window.addEventListener('scroll', syncScrollState, { passive: true })
    return () => {
      window.cancelAnimationFrame(frame)
      window.removeEventListener('scroll', syncScrollState)
    }
  }, [isHome])

  return (
    <Layout className="site-shell">
      <Header className={isHome ? `site-header home-header${homeScrolled ? ' is-scrolled' : ''}` : 'site-header'}>
        <div className="site-header-inner">
          <Link to="/" className="site-brand">
            <span className="pro-brand-mark">
              <img src="/brand/colorvision-icon.png" alt="" />
            </span>
            <span className="site-brand-copy">
              <span className="brand-eyebrow">INTERNAL PORTAL</span>
              <Typography.Text strong>ColorVision</Typography.Text>
            </span>
          </Link>
          <Menu
            mode="horizontal"
            selectedKeys={[selectedKey(location.pathname)]}
            items={menuItems}
            onClick={(item) => navigate(item.key)}
            className="site-menu"
          />
          <Space className="site-actions">
            <Segmented
              size="small"
              value={mode}
              onChange={(value) => setMode(value as ThemeMode)}
              options={[
                { label: '跟随', value: 'system' },
                { label: <SunOutlined />, value: 'light' },
                { label: <MoonOutlined />, value: 'dark' },
              ]}
            />
            {session?.authenticated ? (
              <>
                <Button
                  type="primary"
                  icon={session.is_admin ? <DashboardOutlined /> : <InboxOutlined />}
                  onClick={() => navigate(session.is_admin ? '/admin' : '/transfer')}
                >
                  {session.is_admin ? '发布管理' : '文件中转'}
                </Button>
                <Button
                  icon={<LogoutOutlined />}
                  onClick={async () => {
                    await logout()
                    await onSessionChanged()
                    navigate('/')
                  }}
                >
                  退出
                </Button>
              </>
            ) : (
              <Button icon={<LoginOutlined />} onClick={() => navigate('/login?next=/transfer')}>
                登录 / 注册
              </Button>
            )}
          </Space>
        </div>
      </Header>
      <Content className={isHome ? 'site-content home-site-content' : 'site-content'}>{children}</Content>
    </Layout>
  )
}
