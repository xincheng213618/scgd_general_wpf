import { ProForm, ProFormRadio } from '@ant-design/pro-components'
import { App, Card, Space, Typography } from 'antd'
import type { ThemeMode, ThemeSettingsFormValues, UiDensity } from '../types/admin'

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
  const { message } = App.useApp()

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
      <Card title="生效范围">
        <Typography.Paragraph type="secondary">
          主题和信息密度保存在当前浏览器中，并统一应用到前台发布站和管理端的表单、按钮、表格等组件。
        </Typography.Paragraph>
      </Card>
    </Space>
  )
}
