import { PlusOutlined } from '@ant-design/icons'
import {
  ModalForm,
  ProFormDateTimePicker,
  ProFormSelect,
  ProFormText,
  ProFormTextArea,
  ProTable,
  type ActionType,
  type ProColumns,
} from '@ant-design/pro-components'
import { Alert, App, Button, Popconfirm, Space, Tag, Typography } from 'antd'
import { useRef } from 'react'
import { createApiKey, listApiKeys, revokeApiKey, rotateApiKey } from '../services/admin'
import type { ApiKeyFormValues, ApiKeyItem } from '../types/admin'
import { effectiveApiKeyStatus, toUtcExpiry } from '../utils/apiKeyStatus'
import { shortDate } from '../utils/format'

const scopeOptions = [
  'admin:*',
  'cache:read',
  'cache:refresh',
  'jobs:read',
  'jobs:write',
  'stats:read',
  'plugin:read',
  'plugin:publish',
  'release:publish',
  'file:transfer',
  'copilot:config:read',
].map((value) => ({ label: value, value }))

export function ApiKeysPage() {
  const { message, modal } = App.useApp()
  const actionRef = useRef<ActionType>(null)

  const statusMeta = {
    active: { label: '有效', color: 'green' },
    expired: { label: '已过期', color: 'gold' },
    revoked: { label: '已撤销', color: 'default' },
    invalid_expiry: { label: '时间异常', color: 'red' },
  } as const

  const columns: ProColumns<ApiKeyItem>[] = [
    {
      title: '名称',
      dataIndex: 'name',
      copyable: true,
    },
    {
      title: '前缀',
      dataIndex: 'key_prefix',
      width: 120,
      render: (_, record) => <Typography.Text code>{record.key_prefix}</Typography.Text>,
    },
    {
      title: '权限范围',
      dataIndex: 'scopes',
      search: false,
      render: (_, record) => (
        <Space wrap size={[4, 4]}>
          {(record.scopes || '').split(',').filter(Boolean).map((scope) => (
            <Tag color={scope === 'admin:*' ? 'red' : 'blue'} key={scope}>
              {scope}
            </Tag>
          ))}
        </Space>
      ),
    },
    {
      title: '状态',
      dataIndex: 'status',
      width: 110,
      search: false,
      render: (_, record) => {
        const meta = statusMeta[effectiveApiKeyStatus(record)]
        return <Tag color={meta.color}>{meta.label}</Tag>
      },
    },
    {
      title: '到期时间',
      dataIndex: 'expires_at',
      width: 170,
      search: false,
      render: (_, record) => (
        <Typography.Text type={effectiveApiKeyStatus(record) === 'active' ? 'secondary' : 'danger'}>
          {record.expires_at ? shortDate(record.expires_at) : '永不过期'}
        </Typography.Text>
      ),
    },
    {
      title: '最后使用',
      dataIndex: 'last_used_at',
      width: 170,
      search: false,
      render: (_, record) => record.last_used_at ? shortDate(record.last_used_at) : '从未使用',
    },
    {
      title: '创建时间',
      dataIndex: 'created_at',
      valueType: 'dateTime',
      width: 180,
      search: false,
    },
    {
      title: '操作',
      valueType: 'option',
      width: 190,
      render: (_, record) => (
        <Space>
          <Popconfirm
            title="确认轮换该 API Key？旧 Key 会被撤销。"
            onConfirm={async () => {
              const result = await rotateApiKey(record.id)
              modal.success({
                title: '新 API Key',
                content: <Typography.Text copyable code>{result.key}</Typography.Text>,
              })
              actionRef.current?.reload()
            }}
          >
            <Button size="small" disabled={effectiveApiKeyStatus(record) !== 'active'}>轮换</Button>
          </Popconfirm>
          <Popconfirm
            title="确认撤销该 API Key？"
            onConfirm={async () => {
              await revokeApiKey(record.id)
              message.success('API Key 已撤销')
              actionRef.current?.reload()
            }}
          >
            <Button size="small" danger disabled={effectiveApiKeyStatus(record) === 'revoked'}>
              撤销
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Alert
        type="info"
        showIcon
        message="API Key 只在创建或轮换后显示一次"
        description="到期时间由服务端按 UTC 校验；已过期或时间异常的 Key 会立即拒绝认证。最后使用时间最多每分钟更新一次。"
      />
      <ProTable<ApiKeyItem>
        actionRef={actionRef}
        rowKey="id"
        columns={columns}
        request={async () => {
          const data = await listApiKeys()
          return { data, success: true, total: data.length }
        }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        scroll={{ x: 1280 }}
        headerTitle="API Key"
        toolBarRender={() => [
          <ModalForm<ApiKeyFormValues>
            key="create"
            title="创建 API Key"
            trigger={<Button type="primary" icon={<PlusOutlined />}>新建</Button>}
            modalProps={{ destroyOnHidden: true }}
            initialValues={{ scopes: ['stats:read'] }}
            onFinish={async (values) => {
              const result = await createApiKey({
                name: values.name,
                description: values.description,
                scopes: values.scopes.join(','),
                expires_at: toUtcExpiry(values.expires_at),
              })
              modal.success({
                title: '请立即保存 API Key',
                content: <Typography.Text copyable code>{result.key}</Typography.Text>,
              })
              actionRef.current?.reload()
              return true
            }}
          >
            <ProFormText name="name" label="名称" rules={[{ required: true, message: '请输入名称' }]} />
            <ProFormTextArea name="description" label="说明" />
            <ProFormSelect
              name="scopes"
              label="权限范围"
              mode="multiple"
              options={scopeOptions}
              rules={[{ required: true, message: '请选择权限范围' }]}
            />
            <ProFormDateTimePicker
              name="expires_at"
              label="过期时间"
              tooltip="留空时由服务端设置为 90 天后"
              fieldProps={{
                showNow: true,
                disabledDate: (current) => current.endOf('day').valueOf() < Date.now(),
              }}
            />
          </ModalForm>,
        ]}
      />
    </Space>
  )
}
