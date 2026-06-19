import { App as AntApp, ConfigProvider, theme } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AdminLayout } from './layouts/AdminLayout'
import { PublicLayout } from './layouts/PublicLayout'
import { ApiKeysPage } from './pages/ApiKeysPage'
import { AuditPage } from './pages/AuditPage'
import { BrowsePage } from './pages/BrowsePage'
import { CachePage } from './pages/CachePage'
import { ChangelogPage } from './pages/ChangelogPage'
import { Dashboard } from './pages/Dashboard'
import { FilesPage } from './pages/FilesPage'
import { HomePage } from './pages/HomePage'
import { JobsPage } from './pages/JobsPage'
import { LoginPage } from './pages/LoginPage'
import { PluginDetailPage } from './pages/PluginDetailPage'
import { PluginsPage } from './pages/PluginsPage'
import { PublishPage } from './pages/PublishPage'
import { ReleasesPage } from './pages/ReleasesPage'
import { SettingsPage } from './pages/SettingsPage'
import { ToolsPage } from './pages/ToolsPage'
import { UpdatesPage } from './pages/UpdatesPage'
import { getSession } from './services/auth'
import type { ThemeMode } from './types/admin'
import type { AuthSession } from './types/site'

const themeStorageKey = 'colorvision-web-theme'

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

  return (
    <ConfigProvider theme={configTheme}>
      <AntApp>
        <BrowserRouter>
          <Routes>
            <Route
              path="/"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <HomePage />
                </PublicLayout>
              }
            />
            <Route
              path="/plugins"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <PluginsPage />
                </PublicLayout>
              }
            />
            <Route
              path="/plugins/:pluginId"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <PluginDetailPage />
                </PublicLayout>
              }
            />
            <Route
              path="/releases"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <ReleasesPage />
                </PublicLayout>
              }
            />
            <Route
              path="/changelog"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <ChangelogPage />
                </PublicLayout>
              }
            />
            <Route
              path="/updates"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <UpdatesPage />
                </PublicLayout>
              }
            />
            <Route
              path="/tools"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <ToolsPage />
                </PublicLayout>
              }
            />
            <Route
              path="/browse/*"
              element={
                <PublicLayout mode={mode} setMode={setMode} session={session}>
                  <BrowsePage />
                </PublicLayout>
              }
            />
            <Route path="/login" element={<LoginPage onLoggedIn={refreshSession} />} />
            <Route
              path="/admin"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <Dashboard />
                </AdminLayout>
              }
            />
            <Route
              path="/admin/publish"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <PublishPage />
                </AdminLayout>
              }
            />
            <Route
              path="/admin/files"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <FilesPage />
                </AdminLayout>
              }
            />
            <Route
              path="/admin/cache"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <CachePage />
                </AdminLayout>
              }
            />
            <Route
              path="/admin/jobs"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <JobsPage />
                </AdminLayout>
              }
            />
            <Route
              path="/admin/api-keys"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <ApiKeysPage />
                </AdminLayout>
              }
            />
            <Route
              path="/admin/audit"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <AuditPage />
                </AdminLayout>
              }
            />
            <Route
              path="/admin/settings"
              element={
                <AdminLayout mode={mode} setMode={setMode} resolvedTheme={resolvedTheme}>
                  <SettingsPage mode={mode} setMode={setMode} />
                </AdminLayout>
              }
            />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </AntApp>
    </ConfigProvider>
  )
}

export default App
