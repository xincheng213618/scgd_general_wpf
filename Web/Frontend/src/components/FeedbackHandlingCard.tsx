import { Alert, App, Button, Card, Collapse, Descriptions, Form, Input, Space, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { getFeedbackHandling, saveFeedbackHandling } from '../services/admin'
import type { FeedbackHandling, FeedbackHandlingValues } from '../types/admin'
import { feedbackBeijingTime } from '../utils/feedback'

export function FeedbackHandlingCard({ feedbackId, canManage, onDirtyChange }: {
  feedbackId: string
  canManage: boolean
  onDirtyChange: (dirty: boolean) => void
}) {
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<FeedbackHandlingValues>()
  const [record, setRecord] = useState<FeedbackHandling | null>(null)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [loading, setLoading] = useState(true)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    onDirtyChange(dirty)
    const beforeUnload = (event: BeforeUnloadEvent) => {
      if (dirty) { event.preventDefault(); event.returnValue = '' }
    }
    window.addEventListener('beforeunload', beforeUnload)
    return () => { onDirtyChange(false); window.removeEventListener('beforeunload', beforeUnload) }
  }, [dirty, onDirtyChange])

  useEffect(() => {
    const controller = new AbortController()
    getFeedbackHandling(feedbackId, AbortSignal.any([controller.signal, AbortSignal.timeout(15000)]))
      .then((value) => {
        if (controller.signal.aborted) return
        setRecord(value)
        form.setFieldsValue(value)
        setDirty(false)
        setError('')
      })
      .catch((reason) => {
        if (!controller.signal.aborted) setError(reason instanceof Error ? reason.message : '加载处理记录失败')
      })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [feedbackId, form, reload])

  const reloadRecord = () => {
    const load = () => { setLoading(true); setReload((value) => value + 1) }
    if (dirty) {
      modal.confirm({ title: '放弃未保存的处理记录并重新加载？', okText: '重新加载', cancelText: '继续编辑', onOk: load })
    } else load()
  }

  const save = async (values: FeedbackHandlingValues) => {
    if (!record || saving) return
    setSaving(true)
    setError('')
    try {
      const result = await saveFeedbackHandling(feedbackId, values, record.revision)
      setRecord(result)
      form.setFieldsValue(result)
      setDirty(false)
      message.success(result.changed ? '处理记录已保存' : '处理记录没有变化')
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : '保存失败，请重新加载核对后重试')
    } finally { setSaving(false) }
  }

  return (
    <Card size="small" title="内部处理记录" loading={loading}
      extra={<Button type="link" size="small" disabled={saving || loading} onClick={reloadRecord}>重新加载</Button>}>
      <Space direction="vertical" size="middle" className="wide-space">
        {error && <Alert type="error" showIcon message={error} description="未保存的内容仍保留在表单中。" />}
        {record && <>
          {canManage ? (
            <Form form={form} layout="vertical" onFinish={(values) => void save(values)}
              onValuesChange={() => setDirty(true)} disabled={saving}>
              <Form.Item name="conclusion" label="处理结论" rules={[{ max: 4000 }]}>
                <Input.TextArea rows={3} maxLength={4000} showCount placeholder="原因、采取的措施和处理结果" />
              </Form.Item>
              <Form.Item name="fixed_version" label="修复版本" rules={[{ max: 100 }]}>
                <Input maxLength={100} placeholder="例如 1.4.14.80；无需发版可留空" />
              </Form.Item>
              <Form.Item name="verification" label="验证结果" rules={[{ max: 4000 }]}>
                <Input.TextArea rows={3} maxLength={4000} showCount placeholder="验证环境、步骤和结果；尚未验证请明确记录" />
              </Form.Item>
              <Space wrap>
                <Button type="primary" htmlType="submit" loading={saving} disabled={!dirty}>保存处理记录</Button>
                <Typography.Text type="secondary">{dirty ? '有未保存的修改' : '保存记录后可单独更新反馈状态'}</Typography.Text>
              </Space>
            </Form>
          ) : (
            <Descriptions column={1} size="small">
              <Descriptions.Item label="处理结论"><span style={{ whiteSpace: 'pre-wrap' }}>{record.conclusion || '未填写'}</span></Descriptions.Item>
              <Descriptions.Item label="修复版本">{record.fixed_version || '未填写'}</Descriptions.Item>
              <Descriptions.Item label="验证结果"><span style={{ whiteSpace: 'pre-wrap' }}>{record.verification || '未填写'}</span></Descriptions.Item>
            </Descriptions>
          )}
          <Typography.Text type="secondary">
            {record.updated_at ? `最近修改：${record.actor_id || record.actor_type} · ${feedbackBeijingTime(record.updated_at)}` : '尚无处理记录'}
          </Typography.Text>
          {record.history.length > 0 && <Collapse size="small" items={[{
            key: 'history', label: `修改历史（最近 ${record.history.length} 次，最多保留 20 次）`,
            children: <Space direction="vertical" size="middle" className="wide-space">
              {record.history.map((entry) => <Card key={entry.revision} size="small" title={`第 ${entry.revision} 次修改`}>
                <Typography.Paragraph type="secondary">{entry.actor_id || entry.actor_type} · {feedbackBeijingTime(entry.updated_at || '')}</Typography.Paragraph>
                <Descriptions column={1} size="small">
                  <Descriptions.Item label="处理结论"><span style={{ whiteSpace: 'pre-wrap' }}>{entry.conclusion || '未填写'}</span></Descriptions.Item>
                  <Descriptions.Item label="修复版本">{entry.fixed_version || '未填写'}</Descriptions.Item>
                  <Descriptions.Item label="验证结果"><span style={{ whiteSpace: 'pre-wrap' }}>{entry.verification || '未填写'}</span></Descriptions.Item>
                </Descriptions>
              </Card>)}
            </Space>,
          }]} />}
        </>}
      </Space>
    </Card>
  )
}
