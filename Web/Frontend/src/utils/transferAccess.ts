import type { AuthSession } from '../types/site'
import { hasPermission } from './permissions.ts'

export type TransferAccessState = 'loading' | 'login' | 'password-change' | 'forbidden' | 'ready'

export function getTransferAccessState(session: AuthSession | null): TransferAccessState {
  if (session === null) return 'loading'
  if (session.authenticated && session.must_change_password) return 'password-change'
  if (session.authenticated) {
    return hasPermission(session, 'file:transfer') ? 'ready' : 'forbidden'
  }
  return session.anonymous_transfer_upload_enabled ? 'ready' : 'login'
}

export function getTransferLoginUrl(pathname: string, search = '', hash = '') {
  const next = `${pathname || '/transfer'}${search}${hash}`
  return `/login?${new URLSearchParams({ next }).toString()}`
}
