import { App as AntApp, ConfigProvider, theme } from 'antd'
import { Component, lazy, Suspense, useEffect, useMemo, useState } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { PublicLayout } from './layouts/PublicLayout'
import { getSession } from './services/auth'
import type { ThemeMode, UiDensity } from './types/admin'
import type { AuthSession } from './types/site'

const themeStorageKey = 'colorvision-web-theme'
const densityStorageKey = 'colorvision-web-density'

const documentTitles: Record<string, string> = {
  '/': 'ColorVision - 下载与插件中心',
  '/plugins': '插件市场 - ColorVision',
  '/releases': '版本中心 - ColorVision',
  '/changelog': '更新说明 - ColorVision',
  '/updates': '增量更新 - ColorVision',
  '/tools': '工具下载 - ColorVision',
  '/transfer': '文件中转 - ColorVision',
  '/login': '登录 / 注册 - ColorVision',
  '/admin': '管理控制台 - ColorVision',
  '/admin/publish': '发布中心 - ColorVision',
  '/admin/files': '文件管理 - ColorVision',
  '/admin/cache': '缓存与索引 - ColorVision',
  '/admin/jobs': '任务调度 - ColorVision',
  '/admin/users': '账号管理 - ColorVision',
  '/admin/api-keys': 'API Key - ColorVision',
  '/admin/copilot': 'Copilot 配置 - ColorVision',
  '/admin/audit': '审计日志 - ColorVision',
  '/admin/traffic': '访问统计 - ColorVision',
  '/admin/settings': '系统设置 - ColorVision',
}

function documentTitle(pathname: string) {
  const exactTitle = documentTitles[pathname]
  if (exactTitle) return exactTitle
  if (pathname.startsWith('/plugins/')) return '插件详情 - ColorVision'
  if (pathname.startsWith('/browse')) return '文件浏览 - ColorVision'
  if (pathname.startsWith('/admin')) return '管理控制台 - ColorVision'
  return 'ColorVision'
}

function RouteDocumentTitle() {
  const { pathname } = useLocation()

  useEffect(() => {
    document.title = documentTitle(pathname)
  }, [pathname])

  return null
}

const AdminLayout = lazy(() => import('./layouts/AdminLayout').then((module) => ({ default: module.AdminLayout })))
const ApiKeysPage = lazy(() => import('./pages/ApiKeysPage').then((module) => ({ default: module.ApiKeysPage })))
const AuditPage = lazy(() => import('./pages/AuditPage').then((module) => ({ default: module.AuditPage })))
const BrowsePage = lazy(() => import('./pages/BrowsePage').then((module) => ({ default: module.BrowsePage })))
const CachePage = lazy(() => import('./pages/CachePage').then((module) => ({ default: module.CachePage })))
const ChangelogPage = lazy(() => import('./pages/ChangelogPage').then((module) => ({ default: module.ChangelogPage })))
const CopilotConfigPage = lazy(() => import('./pages/CopilotConfigPage').then((module) => ({ default: module.CopilotConfigPage })))
const Dashboard = lazy(() => import('./pages/Dashboard').then((module) => ({ default: module.Dashboard })))
const FilesPage = lazy(() => import('./pages/FilesPage').then((module) => ({ default: module.FilesPage })))
const HomePage = lazy(() => import('./pages/HomePage').then((module) => ({ default: module.HomePage })))
const JobsPage = lazy(() => import('./pages/JobsPage').then((module) => ({ default: module.JobsPage })))
const UsersPage = lazy(() => import('./pages/UsersPage').then((module) => ({ default: module.UsersPage })))
const LoginPage = lazy(() => import('./pages/LoginPage').then((module) => ({ default: module.LoginPage })))
const PluginDetailPage = lazy(() => import('./pages/PluginDetailPage').then((module) => ({ default: module.PluginDetailPage })))
const PluginsPage = lazy(() => import('./pages/PluginsPage').then((module) => ({ default: module.PluginsPage })))
const PublishPage = lazy(() => import('./pages/PublishPage').then((module) => ({ default: module.PublishPage })))
const ReleasesPage = lazy(() => import('./pages/ReleasesPage').then((module) => ({ default: module.ReleasesPage })))
const SettingsPage = lazy(() => import('./pages/SettingsPage').then((module) => ({ default: module.SettingsPage })))
const ToolsPage = lazy(() => import('./pages/ToolsPage').then((module) => ({ default: module.ToolsPage })))
const TrafficPage = lazy(() => import('./pages/TrafficPage').then((module) => ({ default: module.TrafficPage })))
const TransferPage = lazy(() => import('./pages/TransferPage').then((module) => ({ default: module.TransferPage })))
const UpdatesPage = lazy(() => import('./pages/UpdatesPage').then((module) => ({ default: module.UpdatesPage })))

function RouteFallback() {
  return <div className="route-loading" role="status">页面加载中…</div>
}

class RouteErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Route chunk failed to load', error, info)
  }

  render() {
    if (this.state.failed) {
      return (
        <main className="route-error" role="alert">
          <h1>页面资源加载失败</h1>
          <p>网站可能刚刚完成更新，请重新载入最新版本。</p>
          <button type="button" onClick={() => window.location.reload()}>重新载入</button>
        </main>
      )
    }
    return this.props.children
  }
}

function useResolvedTheme(mode: ThemeMode) {
  const [systemDark, setSystemDark] = useState(false)

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)')
    const sync = () => setSystemDark(media.matches)
    sync()
    media.addEventListener('change', sync)
    return () => media.removeEventListener('change', sync)
  }, [])

  return mode === 'system' ? (systemDark ? 'dark' : 'light') : mode
}

function useThemeMode() {
  const [mode, setModeState] = useState<ThemeMode>(() => {
    const saved = localStorage.getItem(themeStorageKey)
    return saved === 'light' || saved === 'dark' || saved === 'system' ? saved : 'system'
  })

  const setMode = (next: ThemeMode) => {
    localStorage.setItem(themeStorageKey, next)
    setModeState(next)
  }

  return [mode, setMode] as const
}

function useUiDensity() {
  const [density, setDensityState] = useState<UiDensity>(() => {
    const saved = localStorage.getItem(densityStorageKey)
    return saved === 'small' || saved === 'middle' ? saved : 'middle'
  })

  const setDensity = (next: UiDensity) => {
    localStorage.setItem(densityStorageKey, next)
    setDensityState(next)
  }

  return [density, setDensity] as const
}

function App() {
  const [mode, setMode] = useThemeMode()
  const [density, setDensity] = useUiDensity()
  const [session, setSession] = useState<AuthSession | null>(null)
  const resolvedTheme = useResolvedTheme(mode)
  const dark = resolvedTheme === 'dark'

  useEffect(() => {
    document.documentElement.dataset.theme = resolvedTheme
    document.documentElement.dataset.density = density
    document.body.classList.toggle('cv-admin-dark', dark)
  }, [dark, density, resolvedTheme])

  const configTheme = useMemo(
    () => ({
      algorithm: dark ? theme.darkAlgorithm : theme.defaultAlgorithm,
      token: {
        colorPrimary: '#2563eb',
        colorInfo: '#2563eb',
        colorBgLayout: dark ? '#0f1117' : '#eef3f8',
        borderRadius: 8,
        fontFamily:
          '"Segoe UI", "Microsoft YaHei UI", "PingFang SC", system-ui, sans-serif',
      },
    }),
    [dark],
  )

  const refreshSession = async () => {
    try {
      setSession(await getSession())
    } catch {
      setSession({ authenticated: false })
    }
  }

  useEffect(() => {
    let mounted = true
    getSession()
      .then((nextSession) => {
        if (mounted) setSession(nextSession)
      })
      .catch(() => {
        if (mounted) setSession({ authenticated: false })
      })
    return () => {
      mounted = false
    }
  }, [])

  const publicLayout = (
    <PublicLayout mode={mode} setMode={setMode} session={session} onSessionChanged={refreshSession}>
      <Suspense fallback={<RouteFallback />}>
        <Outlet />
      </Suspense>
    </PublicLayout>
  )

  const adminLayout = (
    <Suspense fallback={<RouteFallback />}>
      <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme} onSessionChanged={refreshSession}>
        <Suspense fallback={<RouteFallback />}>
          <Outlet />
        </Suspense>
      </AdminLayout>
    </Suspense>
  )

  return (
    <ConfigProvider componentSize={density} theme={configTheme}>
      <AntApp>
        <BrowserRouter>
          <RouteDocumentTitle />
          <RouteErrorBoundary>
            <Routes>
            <Route element={publicLayout}>
              <Route index element={<HomePage />} />
              <Route path="plugins" element={<PluginsPage />} />
              <Route path="plugins/:pluginId" element={<PluginDetailPage />} />
              <Route path="releases" element={<ReleasesPage />} />
              <Route path="changelog" element={<ChangelogPage />} />
              <Route path="updates" element={<UpdatesPage />} />
              <Route path="tools" element={<ToolsPage />} />
              <Route path="browse/*" element={<BrowsePage />} />
              <Route path="transfer" element={<TransferPage session={session} />} />
            </Route>
            <Route
              path="/login"
              element={
                <Suspense fallback={<RouteFallback />}>
                  <LoginPage onLoggedIn={refreshSession} />
                </Suspense>
              }
            />
            <Route path="/admin" element={adminLayout}>
              <Route index element={<Dashboard />} />
              <Route path="publish" element={<PublishPage />} />
              <Route path="files" element={<FilesPage />} />
              <Route path="cache" element={<CachePage />} />
              <Route path="jobs" element={<JobsPage />} />
              <Route path="users" element={<UsersPage />} />
              <Route path="api-keys" element={<ApiKeysPage />} />
              <Route path="copilot" element={<CopilotConfigPage />} />
              <Route path="audit" element={<AuditPage />} />
              <Route path="traffic" element={<TrafficPage />} />
              <Route
                path="settings"
                element={(
                  <SettingsPage
                    mode={mode}
                    setMode={setMode}
                    density={density}
                    setDensity={setDensity}
                  />
                )}
              />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </RouteErrorBoundary>
        </BrowserRouter>
      </AntApp>
    </ConfigProvider>
  )
}

export default App
