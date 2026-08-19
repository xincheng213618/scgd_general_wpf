export function describeHttpError(statusCode: number) {
  const descriptions: Record<number, string> = {
    400: '请求参数错误',
    401: '未登录或认证失败',
    403: '权限不足',
    404: '路由或资源不存在',
    405: '请求方法不支持',
    409: '状态冲突',
    413: '请求内容过大',
    422: '请求内容无法处理',
    429: '请求过于频繁',
  }
  if (descriptions[statusCode]) return descriptions[statusCode]
  if (statusCode >= 500) return '服务端错误'
  if (statusCode >= 400) return '请求侧错误'
  return '非错误响应'
}
