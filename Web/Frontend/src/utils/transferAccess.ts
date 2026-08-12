import type { AuthSession } from '../types/site'

export type TransferAccessState = 'loading' | 'login' | 'ready'

export function getTransferAccessState(session: AuthSession | null): TransferAccessState {
  if (session === null) return 'loading'
  return session.authenticated ? 'ready' : 'login'
}

export function getTransferLoginUrl(pathname: string, search = '', hash = '') {
  const next = `${pathname || '/transfer'}${search}${hash}`
  return `/login?${new URLSearchParams({ next }).toString()}`
}
