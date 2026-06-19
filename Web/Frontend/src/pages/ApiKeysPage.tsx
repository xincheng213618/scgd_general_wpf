import { PlusOutlined } from '@ant-design/icons'
import {
  ModalForm,
  ProFormSelect,
  ProFormText,
  ProFormTextArea,
  ProTable,
  type ActionType,
  type ProColumns,
} from '@ant-design/pro-components'
import { App, Button, Popconfirm, Space, Tag, Typography } from 'antd'
import { useRef } from 'react'
import { createApiKey, listApiKeys, revokeApiKey, rotateApiKey } from '../services/admin'
import type { ApiKeyFormValues, ApiKeyItem } from '../types/admin'

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
].map((value) => ({ label: value, value }))

export function ApiKeysPage() {
  const { message, modal } = App.useApp()
  const actionRef = useRef<ActionType>(null)

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
      dataIndex: 'is_active',
      width: 100,
      render: (_, record) => <Tag color={record.is_active ? 'green' : 'default'}>{record.is_active ? '启用' : '已撤销'}</Tag>,
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
      width: 180,
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
            <Button size="small">轮换</Button>
          </Popconfirm>
          <Popconfirm
            title="确认撤销该 API Key？"
            onConfirm={async () => {
              await revokeApiKey(record.id)
              message.success('API Key 已撤销')
              actionRef.current?.reload()
            }}
          >
            <Button size="small" danger disabled={!record.is_active}>
              撤销
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
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
              expires_at: values.expires_at,
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
          <ProFormText name="expires_at" label="过期时间" placeholder="可选，ISO 时间字符串" />
        </ModalForm>,
      ]}
    />
  )
}
