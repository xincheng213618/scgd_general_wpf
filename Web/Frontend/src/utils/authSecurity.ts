import type { AuthSession } from '../types/site'

const MAX_RETRY_SECONDS = 24 * 60 * 60

export function normalizeRetryAfter(value: unknown): number {
  const numeric = typeof value === 'number' ? value : Number(value)
  if (!Number.isFinite(numeric) || numeric <= 0) return 0
  return Math.min(MAX_RETRY_SECONDS, Math.ceil(numeric))
}

export function formatLoginRetryCountdown(value: number): string {
  const seconds = normalizeRetryAfter(value)
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  const remainder = seconds % 60
  const minuteText = String(minutes).padStart(2, '0')
  const secondText = String(remainder).padStart(2, '0')
  return hours > 0
    ? `${String(hours).padStart(2, '0')}:${minuteText}:${secondText}`
    : `${minuteText}:${secondText}`
}

export function loginLockRemainingSeconds(
  lockedUntil: string | null | undefined,
  nowMs = Date.now(),
): number {
  if (!lockedUntil) return 0
  const unlockAt = new Date(lockedUntil).getTime()
  if (!Number.isFinite(unlockAt)) return 0
  return normalizeRetryAfter((unlockAt - nowMs) / 1000)
}

export function sessionAfterPasswordChange(
  session: AuthSession,
  result: { must_change_password: boolean },
): AuthSession {
  return {
    ...session,
    must_change_password: result.must_change_password,
  }
}
