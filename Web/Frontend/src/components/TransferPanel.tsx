import { DeleteOutlined, InboxOutlined, ReloadOutlined } from '@ant-design/icons'
import { App, Button, Card, Space, Table, Tag } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useCallback, useEffect, useState } from 'react'
import { deleteTransferFile, getTransferFiles, uploadTransferFile } from '../services/site'
import { AuthRequiredError } from '../services/request'
import type { TransferFile, TransferFilesResponse } from '../types/site'
import { humanSize, shortDate } from '../utils/format'
import { UploadProgress } from './UploadProgress'

export function TransferPanel({ onAuthRequired }: { onAuthRequired?: () => void }) {
  const { message } = App.useApp()
  const [data, setData] = useState<TransferFilesResponse | null>(null)
  const [file, setFile] = useState<File | null>(null)
  const [progress, setProgress] = useState(0)
  const [uploading, setUploading] = useState(false)

  const handleError = useCallback(
    (error: unknown, fallback: string) => {
      if (error instanceof AuthRequiredError && onAuthRequired) {
        onAuthRequired()
        return
      }
      message.error(error instanceof Error ? error.message : fallback)
    },
    [message, onAuthRequired],
  )

  const load = useCallback(
    () => getTransferFiles().then(setData).catch((error) => handleError(error, '中转文件加载失败')),
    [handleError],
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
              try {
                await deleteTransferFile(record.name)
                message.success('已删除')
                await load()
              } catch (error) {
                handleError(error, '删除失败')
              }
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
          <input
            type="file"
            onChange={(event) => {
              setFile(event.target.files?.[0] || null)
              setProgress(0)
            }}
          />
          <UploadProgress active={uploading} file={file} percent={progress} />
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
                handleError(error, '上传失败')
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
