import {
  ApiOutlined,
  AppstoreOutlined,
  AuditOutlined,
  BarChartOutlined,
  CloudUploadOutlined,
  DashboardOutlined,
  DatabaseOutlined,
  FolderOpenOutlined,
  LogoutOutlined,
  MoonOutlined,
  ReloadOutlined,
  SafetyCertificateOutlined,
  SettingOutlined,
  SunOutlined,
} from '@ant-design/icons'
import type { ProSettings } from '@ant-design/pro-components'
import { PageContainer, ProLayout } from '@ant-design/pro-components'
import { Button, Dropdown, Segmented, Space } from 'antd'
import type { ReactNode } from 'react'
import { NavLink, useLocation, useNavigate } from 'react-router-dom'
import type { ThemeMode } from '../types/admin'

const routeTitles: Record<string, string> = {
  '/admin': '管理控制台',
  '/admin/publish': '发布中心',
  '/admin/files': '文件管理',
  '/admin/cache': '缓存与索引',
  '/admin/jobs': '任务调度',
  '/admin/api-keys': 'API Key',
  '/admin/audit': '审计日志',
  '/admin/traffic': '访问统计',
  '/admin/settings': '系统设置',
}

const route = {
  path: '/admin',
  routes: [
    {
      path: '/admin',
      name: '管理控制台',
      icon: <DashboardOutlined />,
    },
    {
      path: '/admin/publish',
      name: '发布管理',
      icon: <CloudUploadOutlined />,
      routes: [
        {
          path: '/admin/publish',
          name: '发布中心',
          icon: <CloudUploadOutlined />,
        },
        {
          path: '/admin/files',
          name: '文件管理',
          icon: <FolderOpenOutlined />,
        },
      ],
    },
    {
      path: '/admin/operations',
      name: '系统运维',
      icon: <DatabaseOutlined />,
      routes: [
        {
          path: '/admin/cache',
          name: '缓存与索引',
          icon: <DatabaseOutlined />,
        },
        {
          path: '/admin/jobs',
          name: '任务调度',
          icon: <ReloadOutlined />,
        },
        {
          path: '/admin/api-keys',
          name: 'API Key',
          icon: <ApiOutlined />,
        },
        {
          path: '/admin/audit',
          name: '审计日志',
          icon: <AuditOutlined />,
        },
        {
          path: '/admin/traffic',
          name: '访问统计',
          icon: <BarChartOutlined />,
        },
      ],
    },
    {
      path: '/admin/settings',
      name: '系统设置',
      icon: <SettingOutlined />,
    },
  ],
}

const proSettings: Partial<ProSettings> = {
  layout: 'mix',
  navTheme: 'light',
  contentWidth: 'Fluid',
  fixedHeader: true,
  fixSiderbar: true,
  splitMenus: false,
}

export function AdminLayout({
  children,
  mode,
  setMode,
  resolvedTheme,
}: {
  children: ReactNode
  mode: ThemeMode
  setMode: (mode: ThemeMode) => void
  resolvedTheme: 'light' | 'dark'
}) {
  const location = useLocation()
  const navigate = useNavigate()
  const title = routeTitles[location.pathname] ?? '管理控制台'

  return (
    <ProLayout
      title="ColorVision"
      logo={<div className="pro-brand-mark">CV</div>}
      route={route}
      location={{ pathname: location.pathname }}
      token={{
        header: {
          colorBgHeader: resolvedTheme === 'dark' ? '#111827' : '#ffffff',
        },
        sider: {
          colorMenuBackground: resolvedTheme === 'dark' ? '#111827' : '#ffffff',
        },
      }}
      menuItemRender={(item, dom) => {
        if (!item.path || item.children) return dom
        return <NavLink to={item.path}>{dom}</NavLink>
      }}
      onMenuHeaderClick={() => navigate('/admin')}
      actionsRender={() => [
        <Segmented
          key="theme"
          size="small"
          value={mode}
          onChange={(value) => setMode(value as ThemeMode)}
          options={[
            { label: '跟随', value: 'system' },
            { label: <SunOutlined />, value: 'light' },
            { label: <MoonOutlined />, value: 'dark' },
          ]}
        />,
        <Dropdown
          key="user"
          menu={{
            items: [
              { key: 'front', label: <a href="/">前台发布站</a>, icon: <AppstoreOutlined /> },
              { type: 'divider' },
              { key: 'logout', label: <a href="/logout">退出登录</a>, icon: <LogoutOutlined /> },
            ],
          }}
        >
          <Button type="text" icon={<SafetyCertificateOutlined />}>
            管理员
          </Button>
        </Dropdown>,
      ]}
      menuFooterRender={() => (
        <Space direction="vertical" size={4} className="layout-footer-note">
          <span>Web 管理端</span>
          <span>React + Ant Design</span>
        </Space>
      )}
      {...proSettings}
    >
      <PageContainer title={title} className="page-container">
        {children}
      </PageContainer>
    </ProLayout>
  )
}
