import type { UserRole } from '../types/admin'

export const MIN_ACCOUNT_PASSWORD_LENGTH = 6

export const USER_ROLE_OPTIONS: Array<{
  label: string
  value: UserRole
  description: string
}> = [
  {
    label: '普通用户',
    value: 'user',
    description: '仅使用公开页面和文件传输能力',
  },
  {
    label: '管理员',
    value: 'admin',
    description: '可进入管理后台并执行全部管理操作',
  },
]

export function userRoleLabel(role: UserRole): string {
  return role === 'admin' ? '管理员' : '普通用户'
}

export function oppositeUserRole(role: UserRole): UserRole {
  return role === 'admin' ? 'user' : 'admin'
}

export function passwordResetSuccessMessage(currentSessionPreserved: boolean): string {
  return currentSessionPreserved
    ? '密码已更新；其他登录会话已失效'
    : '密码已重置；该账号现有会话已失效'
}
