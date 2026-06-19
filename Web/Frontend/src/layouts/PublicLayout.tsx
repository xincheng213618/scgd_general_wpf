import {
  AppstoreOutlined,
  CloudDownloadOutlined,
  DashboardOutlined,
  FolderOpenOutlined,
  HomeOutlined,
  LoginOutlined,
  MoonOutlined,
  ProductOutlined,
  SunOutlined,
  ToolOutlined,
} from '@ant-design/icons'
import { Button, Layout, Menu, Segmented, Space, Typography } from 'antd'
import type { ReactNode } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import type { ThemeMode } from '../types/admin'
import type { AuthSession } from '../types/site'

const { Header, Content } = Layout

const menuItems = [
  { key: '/', icon: <HomeOutlined />, label: '首页' },
  { key: '/plugins', icon: <AppstoreOutlined />, label: '插件市场' },
  { key: '/releases', icon: <CloudDownloadOutlined />, label: '版本中心' },
  { key: '/updates', icon: <ProductOutlined />, label: '增量更新' },
  { key: '/tools', icon: <ToolOutlined />, label: '工具下载' },
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
}: {
  children: ReactNode
  mode: ThemeMode
  setMode: (mode: ThemeMode) => void
  session: AuthSession | null
}) {
  const location = useLocation()
  const navigate = useNavigate()

  return (
    <Layout className="site-shell">
      <Header className="site-header">
        <Link to="/" className="site-brand">
          <span className="pro-brand-mark">CV</span>
          <span>
            <Typography.Text strong>ColorVision</Typography.Text>
            <Typography.Text type="secondary">发布中心</Typography.Text>
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
            <Button type="primary" icon={<DashboardOutlined />} onClick={() => navigate('/admin')}>
              后台
            </Button>
          ) : (
            <Button icon={<LoginOutlined />} onClick={() => navigate('/login?next=/admin')}>
              登录
            </Button>
          )}
        </Space>
      </Header>
      <Content className="site-content">{children}</Content>
    </Layout>
  )
}
