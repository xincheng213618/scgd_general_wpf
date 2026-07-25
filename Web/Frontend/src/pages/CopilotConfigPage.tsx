import { KeyOutlined, PlusOutlined } from '@ant-design/icons'
import {
  ModalForm,
  ProFormDigit,
  ProFormSelect,
  ProFormSwitch,
  ProFormText,
  ProTable,
  type ActionType,
  type ProColumns,
} from '@ant-design/pro-components'
import { Alert, App, Button, Card, Popconfirm, Space, Tag, Typography } from 'antd'
import { useRef } from 'react'
import {
  createCopilotProfile,
  deleteCopilotProfile,
  listCopilotProfiles,
  updateCopilotProfile,
} from '../services/admin'
import type {
  CopilotProfile,
  CopilotProfilePayload,
  CopilotProviderType,
  CopilotReasoningMode,
  CopilotVendorType,
} from '../types/admin'

const syncEndpoint = `${window.location.origin}/api/copilot/config`

const vendorOptions: Array<{ label: string; value: CopilotVendorType }> = [
  { label: 'DeepSeek', value: 'DeepSeek' },
  { label: 'OpenAI', value: 'OpenAI' },
  { label: 'Claude', value: 'Claude' },
  { label: 'Grok / xAI', value: 'Grok' },
  { label: 'Gemini', value: 'Gemini' },
  { label: '智谱 GLM', value: 'GLM' },
  { label: 'MiniMax', value: 'MiniMax' },
  { label: 'Xiaomi MiMo', value: 'Xiaomi' },
  { label: 'SenseNova', value: 'SenseNova' },
  { label: '自定义', value: 'Custom' },
]

const providerOptions: Array<{ label: string; value: CopilotProviderType }> = [
  { label: 'OpenAI Compatible', value: 'OpenAICompatible' },
  { label: 'Anthropic Compatible', value: 'AnthropicCompatible' },
]

const reasoningOptions: Array<{ label: string; value: CopilotReasoningMode }> = [
  { label: '默认', value: 'Default' },
  { label: '关闭', value: 'Disabled' },
  { label: '开启', value: 'Enabled' },
  { label: '高', value: 'High' },
  { label: '最高', value: 'Max' },
]

const defaultFormValues: CopilotProfilePayload = {
  name: '',
  vendorType: 'DeepSeek',
  providerType: 'AnthropicCompatible',
  baseUrl: 'https://api.deepseek.com/anthropic',
  model: 'deepseek-v4-pro',
  apiKey: '',
  allowInsecureHttp: false,
  reasoningMode: 'Default',
  enabled: true,
  isDefault: false,
  sortOrder: 0,
}

function ProfileFields({ editing }: { editing: boolean }) {
  return (
    <>
      <ProFormText
        name="name"
        label="配置名称"
        rules={[{ required: true, message: '请输入配置名称' }]}
      />
      <ProFormSelect
        name="vendorType"
        label="供应商"
        options={vendorOptions}
        rules={[{ required: true }]}
      />
      <ProFormSelect
        name="providerType"
        label="接口协议"
        options={providerOptions}
        rules={[{ required: true }]}
      />
      <ProFormText
        name="baseUrl"
        label="Base URL"
        placeholder="https://api.example.com/v1"
        rules={[
          { required: true, message: '请输入 Base URL' },
          { type: 'url', message: '请输入完整的 HTTP/HTTPS URL' },
        ]}
      />
      <ProFormText
        name="model"
        label="模型"
        rules={[{ required: true, message: '请输入模型名称' }]}
      />
      <ProFormText.Password
        name="apiKey"
        label="模型 API Key"
        placeholder={editing ? '留空则保留当前密钥' : '请输入模型 API Key'}
        rules={editing ? [] : [{ required: true, message: '请输入模型 API Key' }]}
        fieldProps={{ autoComplete: 'new-password' }}
      />
      <ProFormSelect
        name="reasoningMode"
        label="推理模式"
        options={reasoningOptions}
        rules={[{ required: true }]}
      />
      <ProFormDigit
        name="sortOrder"
        label="排序"
        min={-100000}
        max={100000}
        fieldProps={{ precision: 0 }}
      />
      <ProFormSwitch name="enabled" label="允许下发" />
      <ProFormSwitch name="isDefault" label="默认配置" />
      <ProFormSwitch
        name="allowInsecureHttp"
        label="允许模型密钥走 HTTP"
        tooltip="仅用于可信内网模型服务；远程 HTTP 会明文传输模型密钥和对话内容。"
      />
    </>
  )
}

export function CopilotConfigPage() {
  const { message } = App.useApp()
  const actionRef = useRef<ActionType>(null)

  const columns: ProColumns<CopilotProfile>[] = [
    {
      title: '配置',
      dataIndex: 'name',
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <Space size={6}>
            <Typography.Text strong>{record.name}</Typography.Text>
            {record.isDefault && <Tag color="gold">默认</Tag>}
          </Space>
          <Typography.Text type="secondary">{record.model}</Typography.Text>
        </Space>
      ),
    },
    {
      title: '供应商 / 协议',
      search: false,
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <span>{vendorOptions.find((item) => item.value === record.vendorType)?.label ?? record.vendorType}</span>
          <Typography.Text type="secondary">
            {record.providerType === 'AnthropicCompatible' ? 'Anthropic Compatible' : 'OpenAI Compatible'}
          </Typography.Text>
        </Space>
      ),
    },
    {
      title: 'Base URL',
      dataIndex: 'baseUrl',
      search: false,
      ellipsis: true,
      copyable: true,
    },
    {
      title: '密钥',
      dataIndex: 'hasApiKey',
      width: 90,
      search: false,
      render: (_, record) => (
        <Tag color={record.hasApiKey ? 'green' : 'red'} icon={<KeyOutlined />}>
          {record.hasApiKey ? '已保存' : '缺失'}
        </Tag>
      ),
    },
    {
      title: '下发',
      dataIndex: 'enabled',
      width: 90,
      render: (_, record) => (
        <Tag color={record.enabled ? 'blue' : 'default'}>{record.enabled ? '启用' : '停用'}</Tag>
      ),
    },
    {
      title: '更新时间',
      dataIndex: 'updatedAt',
      valueType: 'dateTime',
      width: 180,
      search: false,
    },
    {
      title: '操作',
      valueType: 'option',
      width: 170,
      render: (_, record) => (
        <Space>
          <ModalForm<CopilotProfilePayload>
            title={`编辑 ${record.name}`}
            trigger={<Button size="small">编辑</Button>}
            modalProps={{ destroyOnHidden: true }}
            initialValues={{ ...record, apiKey: '' }}
            onFinish={async (values) => {
              await updateCopilotProfile(record.id, values)
              message.success('Copilot 配置已更新')
              actionRef.current?.reload()
              return true
            }}
          >
            <ProfileFields editing />
          </ModalForm>
          <Popconfirm
            title={`确认删除“${record.name}”？`}
            description="已同步到客户端的副本会在客户端下次同步时移除。"
            onConfirm={async () => {
              await deleteCopilotProfile(record.id)
              message.success('Copilot 配置已删除')
              actionRef.current?.reload()
            }}
          >
            <Button size="small" danger>删除</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Alert
        type="warning"
        showIcon
        message="当前站点使用 HTTP"
        description="桌面端会默认拒绝通过远程 HTTP 同步，因为同步令牌和模型 API Key 会被明文传输。仅在可信网络中，才在 ColorVision 的 Copilot 设置里显式允许不安全 HTTP 同步。"
      />
      <Card title="桌面端同步接口">
        <Space direction="vertical" size={8}>
          <Typography.Text copyable code>{syncEndpoint}</Typography.Text>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
            在“API Key”页面创建仅包含 <Typography.Text code>copilot:config:read</Typography.Text> 权限的 Key，
            然后把站点地址和 Key 填入 ColorVision Copilot 的“后台同步”页面。接口只返回启用的配置，后台列表接口不会回显模型密钥。
          </Typography.Paragraph>
        </Space>
      </Card>
      <ProTable<CopilotProfile>
        actionRef={actionRef}
        rowKey="id"
        columns={columns}
        request={async () => {
          const data = await listCopilotProfiles()
          return { data, success: true, total: data.length }
        }}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        search={false}
        cardBordered
        headerTitle="Copilot 模型配置"
        toolBarRender={() => [
          <ModalForm<CopilotProfilePayload>
            key="create"
            title="新建 Copilot 配置"
            trigger={<Button type="primary" icon={<PlusOutlined />}>新建配置</Button>}
            modalProps={{ destroyOnHidden: true }}
            initialValues={defaultFormValues}
            onFinish={async (values) => {
              await createCopilotProfile(values)
              message.success('Copilot 配置已创建')
              actionRef.current?.reload()
              return true
            }}
          >
            <ProfileFields editing={false} />
          </ModalForm>,
        ]}
      />
    </Space>
  )
}
