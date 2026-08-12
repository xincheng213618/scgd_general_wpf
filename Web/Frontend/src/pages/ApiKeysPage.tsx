import { EyeOutlined, PlusOutlined } from '@ant-design/icons'
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
import { Alert, App, Button, Descriptions, Drawer, Empty, List, Popconfirm, Space, Spin, Tag, Typography } from 'antd'
import { useEffect, useMemo, useRef, useState } from 'react'
import {
  createApiKey,
  getApiKeyScopeCatalog,
  getApiKeyUsage,
  listApiKeys,
  revokeApiKey,
  rotateApiKey,
} from '../services/admin'
import type {
  ApiKeyAuditActivityItem,
  ApiKeyFormValues,
  ApiKeyItem,
  ApiKeyScopeCatalog,
  ApiKeyUsage,
} from '../types/admin'
import { effectiveApiKeyStatus, toUtcExpiry } from '../utils/apiKeyStatus'
import { apiKeyAuditTarget, groupApiKeyScopeOptions } from '../utils/apiKeyScopes'
import { shortDate } from '../utils/format'

const statusMeta = {
  active: { label: '有效', color: 'green' },
  expired: { label: '已过期', color: 'gold' },
  revoked: { label: '已撤销', color: 'default' },
  invalid_expiry: { label: '时间异常', color: 'red' },
} as const

function statusTag(key: ApiKeyItem) {
  const meta = statusMeta[effectiveApiKeyStatus(key)]
  return <Tag color={meta.color}>{meta.label}</Tag>
}

function renderActivity(item: ApiKeyAuditActivityItem) {
  return (
    <List.Item>
      <List.Item.Meta
        title={(
          <Space wrap>
            <Tag color="blue">{item.action}</Tag>
            <Typography.Text>{apiKeyAuditTarget(item)}</Typography.Text>
            <Typography.Text type="secondary">{shortDate(item.created_at)}</Typography.Text>
          </Space>
        )}
        description={item.detail || '无补充详情'}
      />
    </List.Item>
  )
}

export function ApiKeysPage() {
  const { message, modal } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const usageRequestRef = useRef<AbortController | null>(null)
  const [scopeCatalog, setScopeCatalog] = useState<ApiKeyScopeCatalog | null>(null)
  const [scopeCatalogError, setScopeCatalogError] = useState('')
  const [scopeCatalogLoading, setScopeCatalogLoading] = useState(true)
  const [selectedKey, setSelectedKey] = useState<ApiKeyItem | null>(null)
  const [usage, setUsage] = useState<ApiKeyUsage | null>(null)
  const [usageError, setUsageError] = useState('')
  const [usageLoading, setUsageLoading] = useState(false)
  const scopeOptions = useMemo(
    () => groupApiKeyScopeOptions(scopeCatalog?.items ?? []),
    [scopeCatalog],
  )
  const scopeDefinitions = useMemo(
    () => new Map((scopeCatalog?.items ?? []).map((item) => [item.value, item])),
    [scopeCatalog],
  )

  useEffect(() => {
    const controller = new AbortController()
    void getApiKeyScopeCatalog(controller.signal)
      .then((catalog) => {
        if (!controller.signal.aborted) setScopeCatalog(catalog)
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          setScopeCatalogError(error instanceof Error ? error.message : '权限目录加载失败')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setScopeCatalogLoading(false)
      })
    return () => {
      controller.abort()
      usageRequestRef.current?.abort()
    }
  }, [])

  const openKeyDetails = async (record: ApiKeyItem) => {
    usageRequestRef.current?.abort()
    const controller = new AbortController()
    usageRequestRef.current = controller
    setSelectedKey(record)
    setUsage(null)
    setUsageError('')
    setUsageLoading(true)
    try {
      const result = await getApiKeyUsage(record.id, controller.signal)
      if (!controller.signal.aborted) setUsage(result)
    } catch (error) {
      if (!controller.signal.aborted) {
        setUsageError(error instanceof Error ? error.message : 'API Key 详情加载失败')
      }
    } finally {
      if (!controller.signal.aborted) setUsageLoading(false)
    }
  }

  const closeKeyDetails = () => {
    usageRequestRef.current?.abort()
    usageRequestRef.current = null
    setSelectedKey(null)
    setUsage(null)
    setUsageError('')
    setUsageLoading(false)
  }

  const detail = usage ?? selectedKey

  const columns: ProColumns<ApiKeyItem>[] = [
    {
      title: '名称',
      dataIndex: 'name',
      render: (_, record) => (
        <Space direction="vertical" size={0}>
          <Typography.Text strong copyable>{record.name}</Typography.Text>
          {record.description && (
            <Typography.Text type="secondary" ellipsis={{ tooltip: record.description }}>
              {record.description}
            </Typography.Text>
          )}
        </Space>
      ),
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
        return statusTag(record)
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
      width: 270,
      render: (_, record) => (
        <Space>
          <Button
            size="small"
            icon={<EyeOutlined />}
            onClick={() => void openKeyDetails(record)}
          >
            详情
          </Button>
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
            key={`create-${scopeCatalog ? scopeCatalog.default_scopes.join('-') : 'loading'}`}
            title="创建 API Key"
            trigger={<Button type="primary" icon={<PlusOutlined />}>新建</Button>}
            modalProps={{ destroyOnHidden: true }}
            initialValues={{ scopes: scopeCatalog?.default_scopes ?? [] }}
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
            {scopeCatalogError && (
              <Alert
                type="error"
                showIcon
                message="权限目录加载失败"
                description={scopeCatalogError}
              />
            )}
            <ProFormSelect
              name="scopes"
              label="权限范围"
              mode="multiple"
              options={scopeOptions}
              extra="权限由服务端统一维护。优先授予完成任务所需的最小范围。"
              fieldProps={{ loading: scopeCatalogLoading, disabled: !scopeCatalog }}
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
      <Drawer
        title={selectedKey ? `API Key 详情 · ${selectedKey.name}` : 'API Key 详情'}
        open={Boolean(selectedKey)}
        onClose={closeKeyDetails}
        width="min(680px, 100vw)"
        destroyOnHidden
      >
        <Spin spinning={usageLoading}>
          <Space direction="vertical" size={16} style={{ width: '100%' }}>
            {usageError && (
              <Alert type="error" showIcon message="详情加载失败" description={usageError} />
            )}
            {detail && (
              <Descriptions
                bordered
                size="small"
                column={1}
                items={[
                  { key: 'status', label: '状态', children: statusTag(detail) },
                  { key: 'name', label: '名称', children: detail.name },
                  { key: 'description', label: '说明', children: detail.description || '-' },
                  { key: 'prefix', label: '前缀', children: <Typography.Text code copyable>{detail.key_prefix}</Typography.Text> },
                  {
                    key: 'scopes',
                    label: '权限范围',
                    children: (
                      <Space wrap size={[4, 4]}>
                        {(detail.scopes || '').split(',').filter(Boolean).map((scope) => {
                          const definition = scopeDefinitions.get(scope)
                          return (
                            <Tag color={scope === 'admin:*' ? 'red' : 'blue'} key={scope} title={definition?.description}>
                              {scope}{definition ? ` · ${definition.label}` : ''}
                            </Tag>
                          )
                        })}
                      </Space>
                    ),
                  },
                  { key: 'created', label: '创建时间', children: shortDate(detail.created_at) },
                  { key: 'expires', label: '到期时间', children: detail.expires_at ? shortDate(detail.expires_at) : '永不过期' },
                  { key: 'last-used', label: '最后使用', children: detail.last_used_at ? shortDate(detail.last_used_at) : '从未使用' },
                ]}
              />
            )}
            <Alert
              type="info"
              showIcon
              message="使用时间与审计操作含义不同"
              description="最后使用时间表示凭据最近一次通过认证，最多每分钟更新一次；下方只列出有审计记录的管理写操作，不代表完整请求次数。"
            />
            <Space align="baseline">
              <Typography.Title level={5} style={{ margin: 0 }}>最近审计操作</Typography.Title>
              <Typography.Text type="secondary">
                共 {usage?.audit_activity.total ?? 0} 条
              </Typography.Text>
            </Space>
            {usage && usage.audit_activity.items.length > 0 ? (
              <List
                dataSource={usage.audit_activity.items}
                renderItem={renderActivity}
              />
            ) : !usageLoading && (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="暂无审计操作" />
            )}
          </Space>
        </Spin>
      </Drawer>
    </Space>
  )
}
