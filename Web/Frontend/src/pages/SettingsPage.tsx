import { ProForm, ProFormDigit, ProFormRadio, ProFormSwitch } from '@ant-design/pro-components'
import { Alert, App, Button, Card, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import {
  getAccountSettings,
  getRetentionSettings,
  updateAccountSettings,
  updateRetentionSettings,
} from '../services/admin'
import type {
  AccountSettingsResponse,
  AccountSettingsValues,
  RetentionSettingsResponse,
  RetentionSettingsValues,
  ThemeMode,
  ThemeSettingsFormValues,
  UiDensity,
} from '../types/admin'
import {
  getRetentionSettingChanges,
  retentionSettingDefinitions,
} from '../utils/operationalSettings'

export function SettingsPage({
  mode,
  setMode,
  density,
  setDensity,
}: {
  mode: ThemeMode
  setMode: (mode: ThemeMode) => void
  density: UiDensity
  setDensity: (density: UiDensity) => void
}) {
  const { message, modal } = App.useApp()
  const [accountSettings, setAccountSettings] = useState<AccountSettingsResponse | null>(null)
  const [loadingAccountSettings, setLoadingAccountSettings] = useState(true)
  const [accountSettingsError, setAccountSettingsError] = useState('')
  const [retentionSettings, setRetentionSettings] = useState<RetentionSettingsResponse | null>(null)
  const [loadingRetention, setLoadingRetention] = useState(true)
  const [retentionError, setRetentionError] = useState('')

  const loadRetentionSettings = async (signal?: AbortSignal) => {
    setLoadingRetention(true)
    setRetentionError('')
    try {
      setRetentionSettings(await getRetentionSettings(signal))
    } catch (error) {
      if (signal?.aborted) return
      setRetentionError(error instanceof Error ? error.message : '加载运维保留策略失败')
    } finally {
      if (!signal?.aborted) setLoadingRetention(false)
    }
  }

  const loadAccountSettings = async (signal?: AbortSignal) => {
    setLoadingAccountSettings(true)
    setAccountSettingsError('')
    try {
      setAccountSettings(await getAccountSettings(signal))
    } catch (error) {
      if (signal?.aborted) return
      setAccountSettingsError(error instanceof Error ? error.message : '加载账号访问策略失败')
    } finally {
      if (!signal?.aborted) setLoadingAccountSettings(false)
    }
  }

  useEffect(() => {
    const controller = new AbortController()
    void getAccountSettings(controller.signal)
      .then((response) => {
        if (!controller.signal.aborted) setAccountSettings(response)
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          setAccountSettingsError(error instanceof Error ? error.message : '加载账号访问策略失败')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoadingAccountSettings(false)
      })
    return () => controller.abort()
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void getRetentionSettings(controller.signal)
      .then((response) => {
        if (!controller.signal.aborted) setRetentionSettings(response)
      })
      .catch((error) => {
        if (!controller.signal.aborted) {
          setRetentionError(error instanceof Error ? error.message : '加载运维保留策略失败')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoadingRetention(false)
      })
    return () => controller.abort()
  }, [])

  const saveRetentionSettings = async (values: RetentionSettingsValues) => {
    if (!retentionSettings) return false
    const changes = getRetentionSettingChanges(retentionSettings.values, values)
    if (changes.length === 0) {
      message.info('保留策略没有变化')
      return true
    }

    return new Promise<boolean>((resolve) => {
      modal.confirm({
        title: '确认更新运维保留策略？',
        width: 560,
        okText: '确认保存',
        cancelText: '取消',
        okButtonProps: { danger: changes.some(({ decreasesRetention }) => decreasesRetention) },
        content: (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            {changes.map((change) => (
              <Typography.Text key={change.key}>
                {change.label}：{change.before} → {change.after} {change.unit}
              </Typography.Text>
            ))}
            {changes.some(({ decreasesRetention }) => decreasesRetention) && (
              <Typography.Text type="danger">
                保留范围已缩小；旧数据会在对应发布或清理任务下次执行时删除。
              </Typography.Text>
            )}
          </Space>
        ),
        onOk: async () => {
          try {
            const response = await updateRetentionSettings(values)
            setRetentionSettings(response)
            setRetentionError('')
            message.success('运维保留策略已保存，无需重启服务')
            resolve(true)
          } catch (error) {
            message.error(error instanceof Error ? error.message : '保存运维保留策略失败')
            throw error
          }
        },
        onCancel: () => resolve(false),
      })
    })
  }

  const saveAccountSettings = async (values: AccountSettingsValues) => {
    if (!accountSettings) return false
    if (values.public_registration_enabled === accountSettings.public_registration_enabled) {
      message.info('账号访问策略没有变化')
      return true
    }

    const enabling = values.public_registration_enabled
    return new Promise<boolean>((resolve) => {
      modal.confirm({
        title: enabling ? '确认开放公开注册？' : '确认关闭公开注册？',
        okText: enabling ? '确认开放' : '确认关闭',
        cancelText: '取消',
        okButtonProps: { danger: enabling },
        content: enabling
          ? '任何能访问站点的人都可以创建普通账号并使用文件中转。管理员权限仍只能由管理员授予。'
          : '注册入口会立即隐藏，注册接口也会拒绝请求；现有账号仍可正常登录。',
        onOk: async () => {
          try {
            const response = await updateAccountSettings(values)
            setAccountSettings(response)
            setAccountSettingsError('')
            message.success(`公开注册已${enabling ? '开启' : '关闭'}，无需重启服务`)
            resolve(true)
          } catch (error) {
            message.error(error instanceof Error ? error.message : '保存账号访问策略失败')
            throw error
          }
        },
        onCancel: () => resolve(false),
      })
    })
  }

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card title="外观设置">
        <ProForm<ThemeSettingsFormValues>
          key={`${mode}-${density}`}
          layout="horizontal"
          submitter={{
            searchConfig: { submitText: '保存偏好' },
            resetButtonProps: false,
          }}
          initialValues={{ themeMode: mode, density }}
          onFinish={async (values) => {
            setMode(values.themeMode)
            setDensity(values.density)
            message.success('外观偏好已保存')
            return true
          }}
        >
          <ProFormRadio.Group
            name="themeMode"
            label="主题"
            options={[
              { label: '跟随系统', value: 'system' },
              { label: '浅色', value: 'light' },
              { label: '深色', value: 'dark' },
            ]}
          />
          <ProFormRadio.Group
            name="density"
            label="信息密度"
            options={[
              { label: '标准', value: 'middle' },
              { label: '紧凑', value: 'small' },
            ]}
          />
        </ProForm>
      </Card>
      <Card
        title="账号访问策略"
        extra={accountSettings && (
          <Tag color={accountSettings.public_registration_enabled ? 'orange' : 'green'}>
            公开注册已{accountSettings.public_registration_enabled ? '开启' : '关闭'}
          </Tag>
        )}
        loading={loadingAccountSettings && !accountSettings}
      >
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          {accountSettings && (
            <Alert
              type={accountSettings.public_registration_enabled ? 'warning' : 'success'}
              showIcon
              message={accountSettings.public_registration_enabled
                ? '任何访客都可以创建普通账号'
                : '新账号由管理员统一创建'}
              description="该策略只控制自助注册。现有账号、登录状态以及管理员在账号管理页创建账号的能力不会受到影响。"
            />
          )}
          {accountSettingsError && (
            <Alert
              type="error"
              showIcon
              message="账号访问策略加载失败"
              description={accountSettingsError}
              action={(
                <Button
                  size="small"
                  loading={loadingAccountSettings}
                  onClick={() => void loadAccountSettings()}
                >
                  重试
                </Button>
              )}
            />
          )}
          {accountSettings && (
            <ProForm<AccountSettingsValues>
              key={String(accountSettings.public_registration_enabled)}
              initialValues={{
                public_registration_enabled: accountSettings.public_registration_enabled,
              }}
              onFinish={saveAccountSettings}
              submitter={{
                searchConfig: { submitText: '保存账号策略', resetText: '恢复当前值' },
              }}
            >
              <ProFormSwitch
                name="public_registration_enabled"
                label="允许公开注册"
                extra="关闭时，登录页不显示注册入口，服务端注册接口同时返回拒绝。"
              />
            </ProForm>
          )}
        </Space>
      </Card>
      <Card
        title="运维保留策略"
        extra={<Tag color="green">保存后无需重启</Tag>}
        loading={loadingRetention && !retentionSettings}
      >
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Alert
            type="info"
            showIcon
            message="这里只管理无敏感信息的保留范围"
            description="账号凭据、角色、密钥、存储路径和服务监听配置不会通过此页面读取或修改。发布包数量在下次发布时生效，数据库备份数量在下次备份或管理数据清理时生效，其余策略在对应清理任务下次运行时生效。"
          />
          {retentionError && (
            <Alert
              type="error"
              showIcon
              message="运维保留策略加载失败"
              description={retentionError}
              action={(
                <Button size="small" loading={loadingRetention} onClick={() => void loadRetentionSettings()}>
                  重试
                </Button>
              )}
            />
          )}
          {retentionSettings && (
            <ProForm<RetentionSettingsValues>
              key={Object.values(retentionSettings.values).join('-')}
              layout="vertical"
              initialValues={retentionSettings.values}
              onFinish={saveRetentionSettings}
              submitter={{
                searchConfig: { submitText: '保存保留策略', resetText: '恢复当前值' },
              }}
            >
              <div className="retention-settings-grid">
                {retentionSettingDefinitions.map((definition) => {
                  const limit = retentionSettings.limits[definition.key]
                  return (
                    <ProFormDigit
                      key={definition.key}
                      name={definition.key}
                      label={definition.label}
                      className="retention-setting-field"
                      required
                      extra={`${definition.description} ${definition.applies}生效。`}
                      fieldProps={{
                        min: limit.minimum,
                        max: limit.maximum,
                        precision: 0,
                        addonAfter: definition.unit,
                        style: { width: '100%' },
                      }}
                      rules={[{
                        required: true,
                        type: 'number',
                        min: limit.minimum,
                        max: limit.maximum,
                        message: `请输入 ${limit.minimum} 至 ${limit.maximum} 的整数`,
                      }]}
                    />
                  )
                })}
              </div>
            </ProForm>
          )}
        </Space>
      </Card>
      <Card title="生效范围">
        <Typography.Paragraph type="secondary">
          主题和信息密度保存在当前浏览器中，并统一应用到前台发布站和管理端。账号访问策略和运维保留策略保存在服务端配置中，对所有访客、管理员和后台任务统一生效。
        </Typography.Paragraph>
      </Card>
    </Space>
  )
}
