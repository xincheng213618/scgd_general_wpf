import { ProForm, ProFormDigit, ProFormRadio } from '@ant-design/pro-components'
import { Alert, App, Button, Card, Space, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { getRetentionSettings, updateRetentionSettings } from '../services/admin'
import type {
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
        title="运维保留策略"
        extra={<Tag color="green">保存后无需重启</Tag>}
        loading={loadingRetention && !retentionSettings}
      >
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          <Alert
            type="info"
            showIcon
            message="这里只管理无敏感信息的保留范围"
            description="账号、密钥、存储路径和服务监听配置不会通过此页面读取或修改。发布包数量在下次发布时生效，其余策略在对应清理任务下次运行时生效。"
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
          主题和信息密度保存在当前浏览器中，并统一应用到前台发布站和管理端的表单、按钮、表格等组件。运维保留策略保存在服务端配置中，对所有管理员和后台任务统一生效。
        </Typography.Paragraph>
      </Card>
    </Space>
  )
}
