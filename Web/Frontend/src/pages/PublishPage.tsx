import { CloudUploadOutlined, DeleteOutlined, InboxOutlined, ReloadOutlined } from '@ant-design/icons'
import {
  App,
  Button,
  Card,
  Checkbox,
  Form,
  Input,
  Progress,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useCallback, useEffect, useState } from 'react'
import {
  deleteTransferFile,
  getCvwsContext,
  getTransferFiles,
  getUploadContext,
  publishCvwsPackage,
  publishPluginPackage,
  uploadTransferFile,
} from '../services/site'
import type { CvwsContext, TransferFile, TransferFilesResponse, UploadContext } from '../types/site'
import { humanSize, shortDate } from '../utils/format'

function PluginPublishPanel() {
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [context, setContext] = useState<UploadContext | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [icon, setIcon] = useState<File | null>(null)
  const [submitting, setSubmitting] = useState(false)

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
            try {
              const result = await publishPluginPackage(data)
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
            <input type="file" accept=".cvxp,.zip" onChange={(event) => setFile(event.target.files?.[0] || null)} />
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
          <input type="file" accept=".zip" onChange={(event) => setFile(event.target.files?.[0] || null)} />
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
              try {
                await publishCvwsPackage(data)
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
        </Space>
      </Card>
      <Card title="现有服务包">
        <Table rowKey="fileName" columns={columns} dataSource={context?.packages || []} />
      </Card>
    </Space>
  )
}

function TransferPanel() {
  const { message } = App.useApp()
  const [data, setData] = useState<TransferFilesResponse | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [progress, setProgress] = useState(0)
  const [uploading, setUploading] = useState(false)

  const load = useCallback(
    () => getTransferFiles().then(setData).catch((error) => message.error(error instanceof Error ? error.message : '中转文件加载失败')),
    [message],
  )

  useEffect(() => {
    load()
  }, [load])

  const columns: ColumnsType<TransferFile> = [
    { title: '文件名', dataIndex: 'name' },
    { title: '大小', dataIndex: 'size', width: 120, render: (value) => humanSize(value) },
    { title: '修改时间', dataIndex: 'modified_display', width: 180, render: (value, record) => value || shortDate(record.modified) },
    {
      title: '操作',
      width: 180,
      render: (_, record) => (
        <Space>
          <Button href={record.download_url}>下载</Button>
          <Button
            danger
            icon={<DeleteOutlined />}
            onClick={async () => {
              await deleteTransferFile(record.name)
              message.success('已删除')
              await load()
            }}
          />
        </Space>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} className="wide-space">
      <Card title="上传文件">
        <Space direction="vertical" className="wide-space">
          <Space wrap>
            <Tag>{data?.root || 'Transfer'}</Tag>
            <Tag>文件 {data?.files.length || 0}</Tag>
            <Tag>总大小 {humanSize(data?.total_size)}</Tag>
          </Space>
          <input type="file" onChange={(event) => setFile(event.target.files?.[0] || null)} />
          <Progress percent={Math.round(progress)} />
          <Button
            type="primary"
            icon={<InboxOutlined />}
            loading={uploading}
            onClick={async () => {
              if (!file) {
                message.error('请选择文件')
                return
              }
              setUploading(true)
              setProgress(0)
              try {
                await uploadTransferFile(file, setProgress)
                message.success('上传完成')
                setFile(null)
                await load()
              } catch (error) {
                message.error(error instanceof Error ? error.message : '上传失败')
              } finally {
                setUploading(false)
              }
            }}
          >
            上传
          </Button>
        </Space>
      </Card>
      <Card title="中转文件" extra={<Button icon={<ReloadOutlined />} onClick={load}>刷新</Button>}>
        <Table rowKey="name" columns={columns} dataSource={data?.files || []} />
      </Card>
    </Space>
  )
}

export function PublishPage() {
  return (
    <Tabs
      items={[
        { key: 'plugin', label: '插件包发布', children: <PluginPublishPanel /> },
        { key: 'cvws', label: '服务包发布', children: <CvwsPublishPanel /> },
        { key: 'transfer', label: '文件中转', children: <TransferPanel /> },
      ]}
    />
  )
}
