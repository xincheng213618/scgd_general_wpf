import { CloudUploadOutlined } from '@ant-design/icons'
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Form,
  Input,
  List,
  Progress,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useCallback, useEffect, useState } from 'react'
import { TransferPanel } from '../components/TransferPanel'
import { getPublishIntegrity } from '../services/admin'
import {
  getCvwsContext,
  getUploadContext,
  publishCvwsPackage,
  publishPluginPackage,
} from '../services/site'
import type { PublishIntegrityReport } from '../types/admin'
import type { CvwsContext, UploadContext } from '../types/site'
import { humanSize } from '../utils/format'
import { UploadProgress } from '../components/UploadProgress'

const integrityStatusText = {
  ok: '发布资料齐全',
  warning: '存在提醒项',
  error: '存在缺失项',
}

const integrityStatusType = {
  ok: 'success',
  warning: 'warning',
  error: 'error',
} as const

function PublishIntegrityPanel() {
  const { message } = App.useApp()
  const [report, setReport] = useState<PublishIntegrityReport | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setReport(await getPublishIntegrity())
    } catch (error) {
      message.error(error instanceof Error ? error.message : '发布完整性检查失败')
    } finally {
      setLoading(false)
    }
  }, [message])

  useEffect(() => {
    let mounted = true
    getPublishIntegrity()
      .then((payload) => {
        if (mounted) setReport(payload)
      })
      .catch((error) => {
        if (mounted) message.error(error instanceof Error ? error.message : '发布完整性检查失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
    }
  }, [message])

  const status = report?.status || 'warning'

  return (
    <Card loading={loading} title="发布完整性">
      {report && (
        <Space direction="vertical" size={16} className="wide-space">
          <Alert
            type={integrityStatusType[status]}
            showIcon
            message={integrityStatusText[status]}
            description={`安装包、增量包、发布说明、插件文档和文档站索引的当前检查结果。`}
            action={<Button onClick={load}>重新检查</Button>}
          />
          <Space wrap size={18}>
            <Progress type="circle" percent={report.score} size={86} status={report.status === 'error' ? 'exception' : undefined} />
            <Space wrap>
              <Tag color="green">通过 {report.okCount}</Tag>
              <Tag color="gold">提醒 {report.warningCount}</Tag>
              <Tag color="red">缺失 {report.errorCount}</Tag>
              <Tag>最新版本 {report.app.latestVersion || '-'}</Tag>
              <Tag>插件 {report.plugins.total}</Tag>
              <Tag>文档 {report.docs.indexedDocumentCount}</Tag>
            </Space>
          </Space>
          <List
            size="small"
            dataSource={report.checks}
            renderItem={(item) => (
              <List.Item actions={item.actionHref ? [<a href={item.actionHref} key="fix">处理</a>] : undefined}>
                <List.Item.Meta
                  title={<Space><Tag color={item.status === 'ok' ? 'green' : item.status === 'warning' ? 'gold' : 'red'}>{item.title}</Tag>{item.detail}</Space>}
                />
              </List.Item>
            )}
          />
        </Space>
      )}
    </Card>
  )
}

function PluginPublishPanel() {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [context, setContext] = useState<UploadContext | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [icon, setIcon] = useState<File | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)

  useEffect(() => {
    getUploadContext().then(setContext).catch(() => undefined)
  }, [])

  return (
    <Card>
      <Space direction="vertical" size={16} className="wide-space">
        <Space wrap>
          <Tag icon={<CloudUploadOutlined />}>最大上传 {humanSize(context?.max_upload_size_bytes)}</Tag>
          <Tag>保留包数 {context?.plugin_package_keep_count ?? '-'}</Tag>
        </Space>
        <Form
          form={form}
          layout="vertical"
          onFinish={async (values) => {
            if (!file) {
              message.error('请选择插件包')
              return
            }
            const data = new FormData()
            data.append('package', file)
            if (icon) data.append('icon', icon)
            Object.entries(values).forEach(([key, value]) => {
              if (value !== undefined && value !== null && String(value).trim() !== '') {
                data.append(key, String(value))
              }
            })
            setSubmitting(true)
            setUploadProgress(0)
            try {
              const result = await publishPluginPackage(data, setUploadProgress)
              message.success(`已发布 ${result.pluginId} ${result.version}`)
              form.resetFields()
              setFile(null)
              setIcon(null)
            } catch (error) {
              message.error(error instanceof Error ? error.message : '发布失败')
            } finally {
              setSubmitting(false)
            }
          }}
        >
          <Form.Item label="插件包" required>
            <input
              type="file"
              accept=".cvxp,.zip"
              onChange={(event) => {
                setFile(event.target.files?.[0] || null)
                setUploadProgress(0)
              }}
            />
          </Form.Item>
          <Form.Item name="PluginId" label="插件 ID" rules={[{ required: true, message: '请输入插件 ID' }]}>
            <Input placeholder="Spectrum" />
          </Form.Item>
          <Form.Item name="Version" label="版本号" rules={[{ required: true, message: '请输入版本号' }]}>
            <Input placeholder="1.0.0.1" />
          </Form.Item>
          <Form.Item name="Name" label="名称">
            <Input />
          </Form.Item>
          <Form.Item name="Description" label="描述">
            <Input.TextArea rows={3} />
          </Form.Item>
          <Form.Item name="Author" label="作者">
            <Input />
          </Form.Item>
          <Form.Item name="Category" label="分类">
            <Input />
          </Form.Item>
          <Form.Item name="RequiresVersion" label="最低 ColorVision 版本">
            <Input />
          </Form.Item>
          <Form.Item name="ChangeLog" label="更新说明">
            <Input.TextArea rows={4} />
          </Form.Item>
          <Form.Item label="图标">
            <input type="file" accept="image/*" onChange={(event) => setIcon(event.target.files?.[0] || null)} />
          </Form.Item>
          <UploadProgress active={submitting} file={file} percent={uploadProgress} />
          <Button type="primary" htmlType="submit" loading={submitting}>
            发布插件包
          </Button>
        </Form>
      </Space>
    </Card>
  )
}

function CvwsPublishPanel() {
  const { message } = App.useApp()
  const [context, setContext] = useState<CvwsContext | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [version, setVersion] = useState('')
  const [setLatest, setSetLatest] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)

  const load = useCallback(() => getCvwsContext().then(setContext).catch(() => undefined), [])

  useEffect(() => {
    load()
  }, [load])

  const columns: ColumnsType<CvwsContext['packages'][number]> = [
    { title: '文件名', dataIndex: 'fileName' },
    { title: '版本', dataIndex: 'version', width: 140 },
    { title: '大小', dataIndex: 'size', width: 120, render: (value) => humanSize(value) },
    { title: '修改时间', dataIndex: 'modifiedDisplay', width: 180 },
    { title: '操作', width: 100, render: (_, record) => <Button href={record.downloadUrl}>下载</Button> },
  ]

  return (
    <Space direction="vertical" size={16} className="wide-space">
      <Card>
        <Space direction="vertical" className="wide-space">
          <Space wrap>
            <Tag>当前版本 {context?.latest_version || '未设置'}</Tag>
            <Tag>服务包 {context?.package_count ?? 0}</Tag>
            <Typography.Text type="secondary">{context?.tool_dir_display}</Typography.Text>
          </Space>
          <input
            type="file"
            accept=".zip"
            onChange={(event) => {
              setFile(event.target.files?.[0] || null)
              setUploadProgress(0)
            }}
          />
          <Input placeholder="版本号，可留空从文件名推断" value={version} onChange={(event) => setVersion(event.target.value)} />
          <Checkbox checked={setLatest} onChange={(event) => setSetLatest(event.target.checked)}>设为最新版本</Checkbox>
          <Button
            type="primary"
            loading={submitting}
            onClick={async () => {
              if (!file) {
                message.error('请选择服务包')
                return
              }
              const data = new FormData()
              data.append('package', file)
              data.append('version', version)
              data.append('set_latest', String(setLatest))
              setSubmitting(true)
              setUploadProgress(0)
              try {
                await publishCvwsPackage(data, setUploadProgress)
                message.success('服务包已发布')
                setFile(null)
                setVersion('')
                await load()
              } catch (error) {
                message.error(error instanceof Error ? error.message : '发布失败')
              } finally {
                setSubmitting(false)
              }
            }}
          >
            发布服务包
          </Button>
          <UploadProgress active={submitting} file={file} percent={uploadProgress} />
        </Space>
      </Card>
      <Card title="现有服务包">
        <Table rowKey="fileName" columns={columns} dataSource={context?.packages || []} />
      </Card>
    </Space>
  )
}

export function PublishPage() {
  return (
    <Space direction="vertical" size={16} className="page-stack">
      <PublishIntegrityPanel />
      <Tabs
        items={[
          { key: 'plugin', label: '插件包发布', children: <PluginPublishPanel /> },
          { key: 'cvws', label: '服务包发布', children: <CvwsPublishPanel /> },
          { key: 'transfer', label: '文件中转', children: <TransferPanel /> },
        ]}
      />
    </Space>
  )
}
