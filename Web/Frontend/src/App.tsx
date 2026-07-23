import { App as AntApp, ConfigProvider, theme } from 'antd'
import { Component, lazy, Suspense, useEffect, useMemo, useState } from 'react'
import type { ErrorInfo, ReactNode } from 'react'
import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { PublicLayout } from './layouts/PublicLayout'
import { getSession } from './services/auth'
import type { ThemeMode } from './types/admin'
import type { AuthSession } from './types/site'

const themeStorageKey = 'colorvision-web-theme'

const AdminLayout = lazy(() => import('./layouts/AdminLayout').then((module) => ({ default: module.AdminLayout })))
const ApiKeysPage = lazy(() => import('./pages/ApiKeysPage').then((module) => ({ default: module.ApiKeysPage })))
const AuditPage = lazy(() => import('./pages/AuditPage').then((module) => ({ default: module.AuditPage })))
const BrowsePage = lazy(() => import('./pages/BrowsePage').then((module) => ({ default: module.BrowsePage })))
const CachePage = lazy(() => import('./pages/CachePage').then((module) => ({ default: module.CachePage })))
const ChangelogPage = lazy(() => import('./pages/ChangelogPage').then((module) => ({ default: module.ChangelogPage })))
const Dashboard = lazy(() => import('./pages/Dashboard').then((module) => ({ default: module.Dashboard })))
const FilesPage = lazy(() => import('./pages/FilesPage').then((module) => ({ default: module.FilesPage })))
const HomePage = lazy(() => import('./pages/HomePage').then((module) => ({ default: module.HomePage })))
const JobsPage = lazy(() => import('./pages/JobsPage').then((module) => ({ default: module.JobsPage })))
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

function App() {
  const [mode, setMode] = useThemeMode()
  const [session, setSession] = useState<AuthSession | null>(null)
  const resolvedTheme = useResolvedTheme(mode)
  const dark = resolvedTheme === 'dark'

  useEffect(() => {
    document.documentElement.dataset.theme = resolvedTheme
    document.body.classList.toggle('cv-admin-dark', dark)
  }, [dark, resolvedTheme])

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
      <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
        <Suspense fallback={<RouteFallback />}>
          <Outlet />
        </Suspense>
      </AdminLayout>
    </Suspense>
  )

  return (
    <ConfigProvider theme={configTheme}>
      <AntApp>
        <BrowserRouter>
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
              <Route path="api-keys" element={<ApiKeysPage />} />
              <Route path="audit" element={<AuditPage />} />
              <Route path="traffic" element={<TrafficPage />} />
              <Route path="settings" element={<SettingsPage mode={mode} setMode={setMode} />} />
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
