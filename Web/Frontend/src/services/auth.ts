import type { AuthSession } from '../types/site'
import { getJson, postJson } from './request'

export function getSession() {
  return getJson<AuthSession>('/api/auth/session')
}

export function login(payload: { username: string; password: string; next?: string }) {
  return postJson<AuthSession & { next?: string }>('/api/auth/login', payload)
}

export function register(payload: { username: string; password: string; next?: string }) {
  return postJson<AuthSession & { next?: string }>('/api/auth/register', payload)
}

export function logout() {
  return postJson<AuthSession>('/api/auth/logout')
}
