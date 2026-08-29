import type { AuthSession } from '../types/site'
import { canOpenAdminRoute, hasPermission } from './permissions.ts'

export type AuthEntryMode = 'login' | 'register'

export const REGISTRATION_WELCOME_PATH = '/account?welcome=1'

export function shouldShowRegistrationWelcome(value: string | null): boolean {
  return value === '1'
}

export function resolveAuthEntryMode(
  requestedMode: string | null,
  publicRegistrationEnabled: boolean,
): AuthEntryMode {
  return requestedMode === 'register' && publicRegistrationEnabled ? 'register' : 'login'
}

export function publicAuthEntryLabel(publicRegistrationEnabled: boolean): string {
  return publicRegistrationEnabled ? '登录 / 注册' : '登录'
}

export function authEntryDescription(publicRegistrationEnabled: boolean): string {
  return publicRegistrationEnabled
    ? '注册账号当前与管理员拥有相同功能权限，后续可在权限管理中调整。'
    : '使用管理员创建的 ColorVision 账号安全登录。'
}

export function shouldShowRegistrationDisabledNotice(
  requestedMode: string | null,
  publicRegistrationEnabled: boolean,
): boolean {
  return requestedMode === 'register' && !publicRegistrationEnabled
}

function isSafeInternalTarget(value: string | null): value is string {
  if (!value || !value.startsWith('/') || value.startsWith('//')) return false
  if ([...value].some((character) => character.charCodeAt(0) < 32 || character.charCodeAt(0) === 127)) {
    return false
  }
  let normalizedPath = value.split(/[?#]/, 1)[0]
  try {
    for (let index = 0; index < 3; index += 1) {
      const decodedPath = decodeURIComponent(normalizedPath)
      if (decodedPath === normalizedPath) break
      normalizedPath = decodedPath
    }
  } catch {
    return false
  }
  return !normalizedPath.startsWith('//') && !normalizedPath.includes('\\')
}

export function authenticatedEntryRedirect(
  session: AuthSession,
  requestedNext: string | null,
): string {
  if (session.must_change_password) return '/account?password_change=required'
  const target = isSafeInternalTarget(requestedNext) ? requestedNext : '/account'
  const targetPath = target.split(/[?#]/, 1)[0]
  if (targetPath === '/login') return '/account'
  if (targetPath?.startsWith('/admin') && !canOpenAdminRoute(session, targetPath)) return '/account'
  if (targetPath === '/transfer' && !hasPermission(session, 'file:transfer')) return '/account'
  return target
}
