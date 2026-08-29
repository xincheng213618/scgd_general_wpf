import { onCLS, onINP, onLCP } from 'web-vitals'
import type { Metric } from 'web-vitals'

export type WebNavigationType = 'hard' | 'spa'
export type CoreWebVitalName = 'CLS' | 'INP' | 'LCP'

export interface PageViewEventPayload {
  kind: 'page_view'
  route: string
  navigationType: WebNavigationType
}

export interface WebVitalEventPayload {
  kind: 'web_vital'
  route: string
  metric: CoreWebVitalName
  value: number
}

type WebExperiencePayload = PageViewEventPayload | WebVitalEventPayload

const endpoint = '/api/v1/analytics/events'
const exactRoutes = new Set([
  '/',
  '/plugins',
  '/releases',
  '/changelog',
  '/updates',
  '/tools',
  '/browse',
  '/transfer',
  '/login',
  '/admin',
  '/admin/publish',
  '/admin/files',
  '/admin/cache',
  '/admin/jobs',
  '/admin/deployments',
  '/admin/feedback',
  '/admin/users',
  '/admin/api-keys',
  '/admin/copilot',
  '/admin/audit',
  '/admin/traffic',
  '/admin/operations/hosts',
  '/admin/settings',
])

let lastTrackedPathname: string | null = null
let pageViewStarted = false
let webVitalsStarted = false
const reportedMetricIds = new Set<string>()

export function normalizeExperienceRoute(pathname: string) {
  const raw = String(pathname || '').trim()
  if (!raw || raw.length > 256 || raw.includes('?') || raw.includes('#') || raw.includes('\\') || raw.includes('//')) {
    return null
  }
  const route = raw === '/' ? raw : raw.replace(/\/+$/, '')
  if (exactRoutes.has(route)) return route
  if (/^\/plugins\/[A-Za-z0-9._-]{1,128}$/.test(route)) return '/plugins/:pluginId'
  if (/^\/transfer\/share\/[0-9a-f]{32}$/i.test(route)) return '/transfer/share/:token'
  if (route.startsWith('/browse/')) return '/browse/*'
  return null
}

export function createPageViewPayload(
  pathname: string,
  navigationType: WebNavigationType,
): PageViewEventPayload | null {
  const route = normalizeExperienceRoute(pathname)
  return route ? { kind: 'page_view', route, navigationType } : null
}

function sendExperience(payload: WebExperiencePayload) {
  const body = JSON.stringify(payload)
  if (typeof navigator.sendBeacon === 'function') {
    const accepted = navigator.sendBeacon(
      endpoint,
      new Blob([body], { type: 'application/json' }),
    )
    if (accepted) return
  }
  void fetch(endpoint, {
    method: 'POST',
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      'X-ColorVision-Web': '1',
    },
    body,
    keepalive: true,
  }).catch(() => undefined)
}

export function trackPageView(pathname: string) {
  const route = normalizeExperienceRoute(pathname)
  const identity = String(pathname || '').replace(/\/+$/, '') || '/'
  if (!route || identity === lastTrackedPathname) return
  const payload = createPageViewPayload(pathname, pageViewStarted ? 'spa' : 'hard')
  if (!payload) return
  lastTrackedPathname = identity
  pageViewStarted = true
  sendExperience(payload)
}

function metricRoute(metric: Metric) {
  try {
    return normalizeExperienceRoute(
      new URL(metric.navigationURL || window.location.href, window.location.href).pathname,
    )
  } catch {
    return null
  }
}

function reportWebVital(metric: Metric) {
  if (metric.name !== 'CLS' && metric.name !== 'INP' && metric.name !== 'LCP') return
  const identity = `${metric.name}:${metric.id}`
  if (reportedMetricIds.has(identity)) return
  const route = metricRoute(metric)
  if (!route || !Number.isFinite(metric.value) || metric.value < 0) return
  reportedMetricIds.add(identity)
  sendExperience({
    kind: 'web_vital',
    route,
    metric: metric.name,
    value: metric.value,
  })
}

export function startWebVitals() {
  if (webVitalsStarted) return
  webVitalsStarted = true
  const options = { reportSoftNavs: true }
  onCLS(reportWebVital, options)
  onINP(reportWebVital, options)
  onLCP(reportWebVital, options)
}
