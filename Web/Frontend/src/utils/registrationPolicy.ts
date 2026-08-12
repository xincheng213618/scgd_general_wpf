export type AuthEntryMode = 'login' | 'register'

export function resolveAuthEntryMode(
  requestedMode: string | null,
  publicRegistrationEnabled: boolean,
): AuthEntryMode {
  return requestedMode === 'register' && publicRegistrationEnabled ? 'register' : 'login'
}

export function publicAuthEntryLabel(publicRegistrationEnabled: boolean): string {
  return publicRegistrationEnabled ? '登录 / 注册' : '登录'
}
