import {
  AppstoreOutlined,
  BookOutlined,
  CloudDownloadOutlined,
  DashboardOutlined,
  EllipsisOutlined,
  FolderOpenOutlined,
  HomeOutlined,
  InboxOutlined,
  LoginOutlined,
  LogoutOutlined,
  MoonOutlined,
  SafetyCertificateOutlined,
  SunOutlined,
  ToolOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { App, Button, Dropdown, Layout, Menu, Segmented, Space } from 'antd'
import { useEffect, useState, type ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { logout } from '../services/auth'
import type { ThemeMode } from '../types/admin'
import type { AuthSession, AuthSessionUpdater } from '../types/site'
import { hasPermission } from '../utils/permissions'
import { publicAuthEntryLabel } from '../utils/registrationPolicy'

const { Header, Content } = Layout

const docsUrl = '/scgd_general_wpf/'

const menuItems: Array<{ key: string; icon: ReactNode; label: string; href?: string }> = [
  { key: '/', icon: <HomeOutlined aria-hidden />, label: '首页' },
  { key: '/plugins', icon: <AppstoreOutlined aria-hidden />, label: '插件市场' },
  { key: '/releases', icon: <CloudDownloadOutlined aria-hidden />, label: '版本中心' },
  { key: '/tools', icon: <ToolOutlined aria-hidden />, label: '工具下载' },
  { key: 'docs', icon: <BookOutlined aria-hidden />, label: '文档中心', href: docsUrl },
  { key: '/transfer', icon: <InboxOutlined aria-hidden />, label: '文件中转' },
  { key: '/browse', icon: <FolderOpenOutlined aria-hidden />, label: '文件浏览' },
]

function selectedKey(pathname: string) {
  if (pathname.startsWith(docsUrl)) return 'docs'
  const match = [...menuItems].reverse().find((item) => item.key !== '/' && pathname.startsWith(item.key))
  return match?.key ?? (pathname === '/' ? '/' : undefined)
}

const publicMenuItems = menuItems.map(({ key, icon, label }) => ({ key, icon, label }))

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
  onSessionChanged: AuthSessionUpdater
}) {
  const { message } = App.useApp()
  const location = useLocation()
  const navigate = useNavigate()
  const isHome = location.pathname === '/'
  const [homeScrolled, setHomeScrolled] = useState(false)
  const [loggingOut, setLoggingOut] = useState(false)
  const activeMenuKey = selectedKey(location.pathname)
  const canUseTransfer = hasPermission(session, 'file:transfer')
  const workspaceTarget = session?.must_change_password
    ? '/account?password_change=required'
    : session?.can_access_admin ? '/admin' : canUseTransfer ? '/transfer' : '/account'
  const showWorkspace = Boolean(
    session?.must_change_password || session?.can_access_admin || canUseTransfer,
  )

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

  useEffect(() => {
    document.body.classList.toggle('cv-home-light', isHome)
    return () => {
      document.body.classList.remove('cv-home-light')
    }
  }, [isHome])

  return (
    <Layout className="site-shell">
      <Header className={isHome ? `site-header home-header${homeScrolled ? ' is-scrolled' : ''}` : 'site-header'}>
        <div className="site-header-inner">
          <Link to="/" className="site-brand" aria-label="ColorVision 首页" title="ColorVision">
            <span className="pro-brand-mark">
              <img src="/brand/colorvision-icon.png" alt="" />
            </span>
          </Link>
          <Menu
            aria-label="主导航"
            mode="horizontal"
            selectedKeys={activeMenuKey ? [activeMenuKey] : []}
            items={publicMenuItems}
            overflowedIndicator={<EllipsisOutlined aria-label="更多导航" />}
            onClick={(item) => {
              const target = menuItems.find((entry) => entry.key === item.key)
              if (target?.href) {
                window.location.href = target.href
                return
              }
              navigate(item.key)
            }}
            className="site-menu"
          />
          <Space className="site-actions">
            {!isHome && (
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
            )}
            {session?.authenticated ? (
              <Dropdown
                menu={{
                  items: [
                    {
                      key: 'account',
                      label: '个人中心',
                      icon: <UserOutlined />,
                    },
                    ...(showWorkspace ? [{
                      key: 'workspace',
                      label: session.must_change_password
                        ? '完成密码修改'
                        : session.can_access_admin ? '管理后台' : '文件中转',
                      icon: session.must_change_password
                        ? <SafetyCertificateOutlined />
                        : session.can_access_admin ? <DashboardOutlined /> : <InboxOutlined />,
                    }] : []),
                    { type: 'divider' },
                    { key: 'logout', label: '退出登录', icon: <LogoutOutlined />, disabled: loggingOut },
                  ],
                  onClick: async ({ key }) => {
                    if (key === 'account') {
                      navigate('/account')
                      return
                    }
                    if (key === 'workspace') {
                      navigate(workspaceTarget)
                      return
                    }
                    if (key !== 'logout') return
                    setLoggingOut(true)
                    try {
                      const nextSession = await logout()
                      await onSessionChanged(nextSession)
                      navigate('/', { replace: true })
                    } catch (error) {
                      message.error(error instanceof Error ? error.message : '退出失败')
                    } finally {
                      setLoggingOut(false)
                    }
                  },
                }}
              >
                <Button type="text" icon={<SafetyCertificateOutlined />} loading={loggingOut}>
                  {session.username || (session.is_admin ? '管理员' : '用户')}
                </Button>
              </Dropdown>
            ) : (
              <Button icon={<LoginOutlined />} onClick={() => navigate('/login?next=/account')}>
                {publicAuthEntryLabel(session?.public_registration_enabled === true)}
              </Button>
            )}
          </Space>
        </div>
      </Header>
      <Content className={isHome ? 'site-content home-site-content' : 'site-content'}>{children}</Content>
    </Layout>
  )
}
