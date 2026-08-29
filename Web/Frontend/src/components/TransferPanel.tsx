import {
  CheckCircleOutlined,
  CloseOutlined,
  CloudUploadOutlined,
  CopyOutlined,
  DeleteOutlined,
  FileOutlined,
  ReloadOutlined,
  RetweetOutlined,
  ShareAltOutlined,
  StopOutlined,
} from '@ant-design/icons'
import { App, Button, Card, Empty, Progress, Space, Table, Tag, Tooltip, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  deleteTransferFile,
  getTransferFiles,
} from '../services/site'
import { AuthRequiredError } from '../services/request'
import { uploadTransferFile, UploadCanceledError } from '../services/transferUpload'
import type { TransferFile, TransferFilesResponse } from '../types/site'
import { humanSize, shortDate } from '../utils/format'

type QueueStatus = 'waiting' | 'uploading' | 'success' | 'error' | 'canceled'

type UploadQueueItem = {
  id: string
  file: File
  status: QueueStatus
  percent: number
  loaded: number
  bytesPerSecond: number
  error?: string
  replaced?: boolean
  resumedFrom?: number
  shareUrl?: string
  expiresAt?: string | null
}

const statusLabels: Record<QueueStatus, string> = {
  waiting: '等待上传',
  uploading: '正在上传',
  success: '上传完成',
  error: '上传失败',
  canceled: '已取消',
}

const statusColors: Record<QueueStatus, string> = {
  waiting: 'default',
  uploading: 'processing',
  success: 'success',
  error: 'error',
  canceled: 'warning',
}

function fileNameKey(name: string) {
  return name.trim().toLocaleLowerCase()
}

function formatRemainingTime(seconds: number) {
  if (!Number.isFinite(seconds) || seconds <= 0) return '即将完成'
  if (seconds < 60) return `约 ${Math.ceil(seconds)} 秒`
  if (seconds < 3600) return `约 ${Math.ceil(seconds / 60)} 分钟`
  return `约 ${(seconds / 3600).toFixed(1)} 小时`
}

async function writeClipboard(text: string) {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text)
    return
  }
  const input = document.createElement('textarea')
  input.value = text
  input.style.position = 'fixed'
  input.style.opacity = '0'
  document.body.appendChild(input)
  input.select()
  const copied = document.execCommand('copy')
  input.remove()
  if (!copied) throw new Error('copy failed')
}

function queueItemMeta(item: UploadQueueItem, anonymousUpload: boolean) {
  if (item.status === 'uploading') {
    const speed = item.bytesPerSecond > 0 ? `${humanSize(item.bytesPerSecond)}/s` : '正在测速'
    const remaining = item.bytesPerSecond > 0
      ? formatRemainingTime((item.file.size - item.loaded) / item.bytesPerSecond)
      : '估算剩余时间'
    const stage = item.percent >= 100 ? '服务器确认中' : `${speed} · 剩余 ${remaining}`
    const resume = item.resumedFrom ? ` · 已从 ${humanSize(item.resumedFrom)} 续传` : ''
    return `${humanSize(item.loaded)} / ${humanSize(item.file.size)} · ${stage}${resume}`
  }
  if (item.status === 'success') {
    const result = anonymousUpload ? '上传完成' : item.replaced ? '已覆盖同名文件' : '已可下载'
    const expiry = item.expiresAt ? ` · 有效至 ${shortDate(item.expiresAt)}` : ''
    return `${humanSize(item.file.size)} · ${result}${item.resumedFrom ? ' · 断点续传完成' : ''}${expiry}`
  }
  if (item.status === 'error') return item.error || '上传失败，请重试'
  if (item.status === 'canceled') return `${humanSize(item.file.size)} · 服务端断点已保留，可继续上传`
  return `${humanSize(item.file.size)} · 等待开始`
}

export function TransferPanel({
  anonymousUpload = false,
  maxUploadBytes,
}: {
  anonymousUpload?: boolean
  maxUploadBytes?: number
} = {}) {
  const { message, modal } = App.useApp()
  const [data, setData] = useState<TransferFilesResponse | null>(null)
  const [queue, setQueue] = useState<UploadQueueItem[]>([])
  const [uploading, setUploading] = useState(false)
  const [dragging, setDragging] = useState(false)
  const [loadingFiles, setLoadingFiles] = useState(false)
  const [deletingFile, setDeletingFile] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const queueIdRef = useRef(0)
  const activeUploadRef = useRef<AbortController | null>(null)
  const cancelQueueRef = useRef(false)

  const handleError = useCallback(
    (error: unknown, fallback: string) => {
      if (error instanceof AuthRequiredError) return
      message.error(error instanceof Error ? error.message : fallback)
    },
    [message],
  )

  const load = useCallback(async () => {
    if (anonymousUpload) {
      setData(null)
      setLoadingFiles(false)
      return
    }
    setLoadingFiles(true)
    try {
      setData(await getTransferFiles())
    } catch (error) {
      handleError(error, '中转文件加载失败')
    } finally {
      setLoadingFiles(false)
    }
  }, [anonymousUpload, handleError])

  const copyShareLink = useCallback(async (shareUrl?: string) => {
    if (!shareUrl) return
    try {
      await writeClipboard(new URL(shareUrl, window.location.origin).href)
      message.success('分享链接已复制')
    } catch {
      message.error('复制失败，请打开分享页后从地址栏复制')
    }
  }, [message])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => () => activeUploadRef.current?.abort(), [])

  useEffect(() => {
    if (!uploading) return
    const warnBeforeLeaving = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', warnBeforeLeaving)
    return () => window.removeEventListener('beforeunload', warnBeforeLeaving)
  }, [uploading])

  const existingNames = useMemo(() => new Set((data?.files || []).map((item) => fileNameKey(item.name))), [data])
  const pendingItems = queue.filter((item) => item.status === 'waiting')
  const completedCount = queue.filter((item) => item.status === 'success').length
  const failedCount = queue.filter((item) => item.status === 'error').length
  const queuedBytes = queue.reduce((total, item) => total + item.file.size, 0)
  const completedBytes = queue.reduce((total, item) => {
    if (item.status === 'success') return total + item.file.size
    if (item.status === 'uploading') return total + item.loaded
    return total
  }, 0)
  const overallPercent = queuedBytes > 0 ? (completedBytes / queuedBytes) * 100 : 0

  const addFiles = (files: File[]) => {
    if (uploading || files.length === 0) return
    const known = new Set(queue.map((item) => fileNameKey(item.file.name)))
    const additions: UploadQueueItem[] = []
    let duplicateCount = 0
    let oversizedCount = 0

    files.forEach((file) => {
      if (anonymousUpload && maxUploadBytes && file.size > maxUploadBytes) {
        oversizedCount += 1
        return
      }
      const nameKey = fileNameKey(file.name)
      if (known.has(nameKey)) {
        duplicateCount += 1
        return
      }
      known.add(nameKey)
      queueIdRef.current += 1
      additions.push({
        id: `transfer-${Date.now()}-${queueIdRef.current}`,
        file,
        status: 'waiting',
        percent: 0,
        loaded: 0,
        bytesPerSecond: 0,
      })
    })

    if (additions.length > 0) setQueue((current) => [...current, ...additions])
    if (duplicateCount > 0) message.info(`已忽略 ${duplicateCount} 个同名文件`)
    if (oversizedCount > 0) message.error(`${oversizedCount} 个文件超过 ${humanSize(maxUploadBytes)} 的访客上传上限`)
  }

  const runUploadQueue = async (items: UploadQueueItem[]) => {
    if (uploading || items.length === 0) return
    const itemIds = new Set(items.map((item) => item.id))
    cancelQueueRef.current = false
    setUploading(true)
    setQueue((current) => current.map((item) => itemIds.has(item.id)
      ? { ...item, status: 'waiting', percent: 0, loaded: 0, bytesPerSecond: 0, error: undefined, resumedFrom: undefined }
      : item))

    let successCount = 0
    for (const queuedItem of items) {
      if (cancelQueueRef.current) break
      const controller = new AbortController()
      activeUploadRef.current = controller
      const startedAt = performance.now()
      setQueue((current) => current.map((item) => item.id === queuedItem.id
        ? { ...item, status: 'uploading', percent: 0, loaded: 0, bytesPerSecond: 0, error: undefined }
        : item))

      try {
        const result = await uploadTransferFile(queuedItem.file, {
          signal: controller.signal,
          onResume: (offset) => {
            setQueue((current) => current.map((item) => item.id === queuedItem.id
              ? { ...item, resumedFrom: offset }
              : item))
          },
          onProgress: ({ loaded, percent }) => {
            const elapsedSeconds = Math.max((performance.now() - startedAt) / 1000, 0.25)
            setQueue((current) => current.map((item) => item.id === queuedItem.id
              ? { ...item, loaded, percent, bytesPerSecond: loaded / elapsedSeconds }
              : item))
          },
        })
        successCount += 1
        setQueue((current) => current.map((item) => item.id === queuedItem.id
          ? {
              ...item,
              status: 'success',
              percent: 100,
              loaded: queuedItem.file.size,
              replaced: result.replaced,
              shareUrl: result.share_url,
              expiresAt: result.expires_at,
              bytesPerSecond: 0,
            }
          : item))
      } catch (error) {
        const canceled = error instanceof UploadCanceledError || controller.signal.aborted
        setQueue((current) => current.map((item) => item.id === queuedItem.id
          ? {
              ...item,
              status: canceled ? 'canceled' : 'error',
              bytesPerSecond: 0,
              error: canceled ? undefined : error instanceof Error ? error.message : '上传失败',
            }
          : item))
        if (error instanceof AuthRequiredError) break
      } finally {
        activeUploadRef.current = null
      }
    }

    if (cancelQueueRef.current) {
      setQueue((current) => current.map((item) => itemIds.has(item.id) && item.status === 'waiting'
        ? { ...item, status: 'canceled' }
        : item))
    }
    setUploading(false)
    await load()
    if (successCount > 0) message.success(`${successCount} 个文件上传完成`)
  }

  const requestUpload = (items: UploadQueueItem[]) => {
    if (uploading) return
    const eligible = items.filter((item) => item.status !== 'success' && item.status !== 'uploading')
    if (eligible.length === 0) {
      message.info('请先添加文件')
      return
    }
    const conflicts = eligible.filter((item) => existingNames.has(fileNameKey(item.file.name)))
    const start = () => void runUploadQueue(eligible)
    if (conflicts.length === 0) {
      start()
      return
    }

    modal.confirm({
      title: `将覆盖 ${conflicts.length} 个同名文件`,
      content: (
        <div className="transfer-conflict-list">
          {conflicts.slice(0, 4).map((item) => <div key={item.id}>{item.file.name}</div>)}
          {conflicts.length > 4 && <div>以及其他 {conflicts.length - 4} 个文件</div>}
        </div>
      ),
      okText: '覆盖并上传',
      cancelText: '返回检查',
      okButtonProps: { danger: true },
      onOk: start,
    })
  }

  const cancelUploads = () => {
    cancelQueueRef.current = true
    activeUploadRef.current?.abort()
    setQueue((current) => current.map((item) => item.status === 'waiting'
      ? { ...item, status: 'canceled' }
      : item))
  }

  const removeQueueItem = (id: string) => {
    setQueue((current) => current.filter((item) => item.id !== id))
  }

  const confirmDelete = (record: TransferFile) => {
    modal.confirm({
      title: '删除中转文件？',
      content: `将永久删除“${record.name}”。`,
      okText: '删除',
      cancelText: '取消',
      okButtonProps: { danger: true },
      async onOk() {
        setDeletingFile(record.name)
        try {
          await deleteTransferFile(record.name)
          message.success('已删除')
          await load()
        } catch (error) {
          handleError(error, '删除失败')
        } finally {
          setDeletingFile(null)
        }
      },
    })
  }

  const columns: ColumnsType<TransferFile> = [
    {
      title: '文件名',
      dataIndex: 'name',
      render: (name: string, record) => (
        <div className="transfer-file-name">
          <span className="transfer-file-icon"><FileOutlined /></span>
          <div>
            <Typography.Text ellipsis={{ tooltip: name }}>{name}</Typography.Text>
            <span>
              {humanSize(record.size)}
              {record.expires_at ? ` · 临时至 ${shortDate(record.expires_at)}` : ''}
            </span>
          </div>
        </div>
      ),
    },
    { title: '大小', dataIndex: 'size', width: 120, responsive: ['md'], render: (value) => humanSize(value) },
    {
      title: '修改时间',
      dataIndex: 'modified_display',
      width: 180,
      responsive: ['md'],
      render: (value, record) => shortDate(record.modified || value),
    },
    {
      title: '操作',
      width: 220,
      render: (_, record) => (
        <Space>
          <Button
            type="link"
            icon={<ShareAltOutlined />}
            onClick={() => void copyShareLink(record.share_url)}
          >
            分享
          </Button>
          <Button type="link" href={record.download_url}>下载</Button>
          <Tooltip title="删除文件">
            <Button
              type="text"
              danger
              icon={<DeleteOutlined />}
              loading={deletingFile === record.name}
              onClick={() => confirmDelete(record)}
              aria-label={`删除 ${record.name}`}
            />
          </Tooltip>
        </Space>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} className="wide-space">
      <Card className="transfer-upload-card">
        <div className="transfer-upload-heading">
          <div>
            <span className="transfer-section-kicker">UPLOAD</span>
            <Typography.Title level={3}>发送文件</Typography.Title>
          </div>
          <span className="transfer-storage-summary">
            {anonymousUpload ? `访客上传 · 24 小时有效 · 单文件 ${humanSize(maxUploadBytes)}` : `${data?.files.length || 0} 个文件 · ${humanSize(data?.total_size)}`}
          </span>
        </div>

        <input
          ref={inputRef}
          className="transfer-file-input"
          type="file"
          multiple
          disabled={uploading}
          onChange={(event) => {
            addFiles(Array.from(event.target.files || []))
            event.target.value = ''
          }}
        />
        <div
          className={`transfer-dropzone${dragging ? ' is-dragging' : ''}${uploading ? ' is-disabled' : ''}`}
          role="button"
          tabIndex={uploading ? -1 : 0}
          aria-disabled={uploading}
          onClick={() => !uploading && inputRef.current?.click()}
          onKeyDown={(event) => {
            if (!uploading && (event.key === 'Enter' || event.key === ' ')) {
              event.preventDefault()
              inputRef.current?.click()
            }
          }}
          onDragEnter={(event) => {
            event.preventDefault()
            if (!uploading) setDragging(true)
          }}
          onDragOver={(event) => event.preventDefault()}
          onDragLeave={(event) => {
            if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setDragging(false)
          }}
          onDrop={(event) => {
            event.preventDefault()
            setDragging(false)
            addFiles(Array.from(event.dataTransfer.files))
          }}
        >
          <span className="transfer-dropzone-icon"><CloudUploadOutlined /></span>
          <strong>{uploading ? '正在上传' : '拖放文件到这里'}</strong>
          {!uploading && <span className="transfer-dropzone-action">选择文件</span>}
          <small>{anonymousUpload ? '无需登录 · 24 小时有效 · 断点续传' : '多文件 · 大文件 · 断点续传'}</small>
        </div>

        {queue.length > 0 && (
          <div className="transfer-queue">
            <div className="transfer-queue-summary">
              <div>
                <strong>{queue.length} 个文件 · {humanSize(queuedBytes)}</strong>
                <span>完成 {completedCount}{failedCount > 0 ? ` · 失败 ${failedCount}` : ''}</span>
              </div>
              <Space wrap>
                {!uploading && failedCount > 0 && (
                  <Button icon={<RetweetOutlined />} onClick={() => requestUpload(queue.filter((item) => item.status === 'error'))}>
                    重试失败项
                  </Button>
                )}
                {!uploading && completedCount > 0 && (
                  <Button onClick={() => setQueue((current) => current.filter((item) => item.status !== 'success'))}>
                    清除已完成
                  </Button>
                )}
                {uploading ? (
                  <Button danger icon={<StopOutlined />} onClick={cancelUploads}>取消上传</Button>
                ) : (
                  <Button
                    type="primary"
                    icon={<CloudUploadOutlined />}
                    disabled={pendingItems.length === 0}
                    onClick={() => requestUpload(pendingItems)}
                  >
                    开始上传{pendingItems.length > 0 ? `（${pendingItems.length}）` : ''}
                  </Button>
                )}
              </Space>
            </div>
            <Progress
              className="transfer-overall-progress"
              percent={Math.round(overallPercent)}
              status={failedCount > 0 && !uploading ? 'exception' : uploading ? 'active' : 'normal'}
              showInfo={uploading || completedCount > 0}
            />
            <div className="transfer-queue-list">
              {queue.map((item) => (
                <div className="transfer-queue-item" key={item.id}>
                  <span className={`transfer-queue-icon is-${item.status}`}>
                    {item.status === 'success' ? <CheckCircleOutlined /> : <FileOutlined />}
                  </span>
                  <div className="transfer-queue-copy">
                    <div className="transfer-queue-name">
                      <Typography.Text ellipsis={{ tooltip: item.file.name }}>{item.file.name}</Typography.Text>
                      {!anonymousUpload && existingNames.has(fileNameKey(item.file.name)) && item.status !== 'success' && <Tag color="orange">将覆盖</Tag>}
                      <Tag color={statusColors[item.status]}>{statusLabels[item.status]}</Tag>
                    </div>
                    <Progress
                      percent={Math.round(item.percent)}
                      status={item.status === 'error' ? 'exception' : item.status === 'success' ? 'success' : 'normal'}
                      showInfo={false}
                      size="small"
                    />
                    <span className={`transfer-queue-meta${item.status === 'error' ? ' is-error' : ''}`}>
                      {queueItemMeta(item, anonymousUpload)}
                    </span>
                  </div>
                  <div className="transfer-queue-actions">
                    {item.status === 'success' && item.shareUrl && (
                      <Tooltip title="复制分享链接">
                        <Button
                          type="link"
                          icon={<CopyOutlined />}
                          onClick={() => void copyShareLink(item.shareUrl)}
                          aria-label={`复制 ${item.file.name} 的分享链接`}
                        >
                          复制链接
                        </Button>
                      </Tooltip>
                    )}
                    {!uploading && (item.status === 'error' || item.status === 'canceled') && (
                      <Tooltip title="重新上传">
                        <Button type="text" icon={<RetweetOutlined />} onClick={() => requestUpload([item])} aria-label={`重新上传 ${item.file.name}`} />
                      </Tooltip>
                    )}
                    {item.status !== 'uploading' && (
                      <Tooltip title="从队列移除">
                        <Button type="text" icon={<CloseOutlined />} onClick={() => removeQueueItem(item.id)} aria-label={`移除 ${item.file.name}`} />
                      </Tooltip>
                    )}
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}
      </Card>

      {!anonymousUpload && <Card
        title={<span className="transfer-files-title">文件 <small>{data?.files.length || 0}</small></span>}
        extra={<Button icon={<ReloadOutlined />} loading={loadingFiles} onClick={() => void load()}>刷新</Button>}
        className="transfer-files-card"
      >
        <Table
          className="transfer-files-table"
          rowKey="name"
          columns={columns}
          dataSource={data?.files || []}
          loading={loadingFiles}
          locale={{ emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="还没有中转文件" /> }}
          pagination={{ pageSize: 10, hideOnSinglePage: true, showSizeChanger: false }}
          scroll={{ x: 620 }}
        />
      </Card>}
    </Space>
  )
}
