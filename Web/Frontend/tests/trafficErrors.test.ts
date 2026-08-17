import assert from 'node:assert/strict'
import test from 'node:test'
import { describeHttpError } from '../src/utils/trafficErrors.ts'

test('common HTTP errors have operator-friendly descriptions', () => {
  assert.equal(describeHttpError(401), '未登录或认证失败')
  assert.equal(describeHttpError(404), '路由或资源不存在')
  assert.equal(describeHttpError(409), '状态冲突')
  assert.equal(describeHttpError(429), '请求过于频繁')
})

test('unknown statuses retain their request or server-side category', () => {
  assert.equal(describeHttpError(418), '请求侧错误')
  assert.equal(describeHttpError(503), '服务端错误')
})
