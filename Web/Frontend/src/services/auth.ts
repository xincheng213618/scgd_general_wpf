import type {
  AccountActivityResponse,
  AccountProfile,
  AuthSession,
  LoginSessionsResponse,
} from '../types/site'
import { deleteJson, getJson, postJson, putJson } from './request'

export function getSession() {
  return getJson<AuthSession>('/api/auth/session')
}

export function login(payload: { username: string; password: string; next?: string }) {
  return postJson<AuthSession & { next?: string }>(
    '/api/auth/login',
    payload,
    { redirectOnUnauthorized: false },
  )
}

export function register(payload: {
  username: string
  password: string
  display_name?: string
  email?: string
  next?: string
}) {
  return postJson<AuthSession & { next?: string }>('/api/auth/register', payload)
}

export function requestPasswordRecovery(identifier: string) {
  return postJson<{ status: 'accepted', message: string }>(
    '/api/auth/password-recovery',
    { identifier },
    { redirectOnUnauthorized: false },
  )
}

export function logout() {
  return postJson<AuthSession>('/api/auth/logout')
}

export function getAccountProfile() {
  return getJson<AccountProfile>('/api/account')
}

export function updateAccountProfile(payload: { display_name: string; email: string }) {
  return putJson<AccountProfile>('/api/account', payload)
}

export function getAccountSessions() {
  return getJson<LoginSessionsResponse>('/api/account/sessions')
}

export function revokeAccountSession(id: string) {
  return deleteJson<{ status: 'revoked'; id: string }>(
    `/api/account/sessions/${encodeURIComponent(id)}`,
  )
}

export function revokeOtherAccountSessions() {
  return deleteJson<{ status: 'revoked'; revoked: number }>('/api/account/sessions/others')
}

export function getAccountActivity(params: { current?: number; pageSize?: number } = {}) {
  const pageSize = params.pageSize ?? 8
  const current = params.current ?? 1
  const search = new URLSearchParams({
    limit: String(pageSize),
    offset: String(Math.max(0, current - 1) * pageSize),
  })
  return getJson<AccountActivityResponse>(`/api/account/activity?${search.toString()}`)
}

export function changeAccountPassword(payload: { current_password: string; new_password: string }) {
  return putJson<{
    status: 'updated'
    current_session_preserved: boolean
    must_change_password: boolean
  }>(
    '/api/account/password',
    payload,
  )
}
