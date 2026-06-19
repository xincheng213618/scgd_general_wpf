import { ProForm, ProFormRadio } from '@ant-design/pro-components'
import { App, Card, Space, Typography } from 'antd'
import type { ThemeMode, ThemeSettingsFormValues } from '../types/admin'

export function SettingsPage({
  mode,
  setMode,
}: {
  mode: ThemeMode
  setMode: (mode: ThemeMode) => void
}) {
  const { message } = App.useApp()

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card title="外观设置">
        <ProForm<ThemeSettingsFormValues>
          layout="horizontal"
          submitter={{
            searchConfig: { submitText: '保存偏好' },
            resetButtonProps: false,
          }}
          initialValues={{ themeMode: mode, density: 'middle' }}
          onFinish={async (values) => {
            setMode(values.themeMode)
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
      <Card title="迁移说明">
        <Typography.Paragraph type="secondary">
          当前设置页已经使用 ProForm。下一阶段可把站点信息、权限角色、发布策略、审计保留和存储配置逐步迁到这里。
        </Typography.Paragraph>
      </Card>
    </Space>
  )
}
