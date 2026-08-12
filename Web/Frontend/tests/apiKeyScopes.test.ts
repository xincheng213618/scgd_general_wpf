import assert from 'node:assert/strict'
import test from 'node:test'
import type { ApiKeyScopeDefinition } from '../src/types/admin.ts'
import { apiKeyAuditTarget, groupApiKeyScopeOptions } from '../src/utils/apiKeyScopes.ts'

const definitions: ApiKeyScopeDefinition[] = [
  {
    value: 'stats:read',
    label: '统计查看',
    description: '读取统计。',
    category: '系统运维',
    access: 'read',
  },
  {
    value: 'ops:relay',
    label: '桌面 Relay',
    description: '桌面端心跳和任务拉取。',
    category: '桌面运维',
    access: 'service',
  },
  {
    value: 'ops:operator',
    label: '运维调度',
    description: '创建桌面运维任务。',
    category: '桌面运维',
    access: 'write',
  },
]

test('scope options preserve server categories and include desktop operations scopes', () => {
  const groups = groupApiKeyScopeOptions(definitions)

  assert.deepEqual(groups.map(({ label }) => label), ['系统运维', '桌面运维'])
  assert.deepEqual(
    groups[1].options.map(({ value }) => value),
    ['ops:relay', 'ops:operator'],
  )
  assert.equal(groups[1].options[0].title, '桌面端心跳和任务拉取。')
})

test('audit target prefers the concrete id and has an explicit empty fallback', () => {
  assert.equal(apiKeyAuditTarget({ action: 'one', target_type: 'release', target_id: '1.2.3' }), '1.2.3')
  assert.equal(apiKeyAuditTarget({ action: 'two', target_type: 'release' }), 'release')
  assert.equal(apiKeyAuditTarget({ action: 'three' }), '-')
})
