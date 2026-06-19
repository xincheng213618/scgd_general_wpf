import { FolderOpenOutlined, SearchOutlined } from '@ant-design/icons'
import { App, Button, Card, Input, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useCallback, useEffect, useState } from 'react'
import { getBrowse } from '../services/site'
import type { BrowsePayload, StorageItem } from '../types/site'
import { downloadPath, humanSize, shortDate } from '../utils/format'

export function FilesPage() {
  const { message } = App.useApp()
  const [subpath, setSubpath] = useState('')
  const [draftPath, setDraftPath] = useState('')
  const [data, setData] = useState<BrowsePayload | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async (path: string) => {
    setLoading(true)
    try {
      const payload = await getBrowse(path)
      setData(payload)
      setSubpath(path)
      setDraftPath(path)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '目录加载失败')
    } finally {
      setLoading(false)
    }
  }, [message])

  useEffect(() => {
    let mounted = true
    getBrowse('')
      .then((payload) => {
        if (!mounted) return
        setData(payload)
        setSubpath('')
        setDraftPath('')
      })
      .catch((error) => {
        if (mounted) message.error(error instanceof Error ? error.message : '目录加载失败')
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })
    return () => {
      mounted = false
    }
  }, [message])

  const columns: ColumnsType<StorageItem> = [
    {
      title: '名称',
      dataIndex: 'name',
      render: (value, record) =>
        record.is_dir ? (
          <Button type="link" onClick={() => load(record.relative_path)}>
            {value}
          </Button>
        ) : (
          <Typography.Text strong>{value}</Typography.Text>
        ),
    },
    { title: '类型', dataIndex: 'is_dir', width: 100, render: (value) => <Tag>{value ? '目录' : '文件'}</Tag> },
    { title: '大小', dataIndex: 'size', width: 120, render: (value) => humanSize(value) },
    { title: '修改时间', dataIndex: 'modified', width: 180, render: (value) => shortDate(value) },
    {
      title: '操作',
      width: 140,
      render: (_, record) => (
        <Button href={record.is_dir ? `/browse/${record.relative_path}` : downloadPath(record.relative_path)}>
          {record.is_dir ? '前台打开' : '下载'}
        </Button>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card>
        <Space direction="vertical" className="wide-space">
          <Space wrap>
            <Tag icon={<FolderOpenOutlined />} color="blue">{subpath || 'Storage Root'}</Tag>
            <Tag>目录 {data?.summary.directory_count || 0}</Tag>
            <Tag>文件 {data?.summary.file_count || 0}</Tag>
            <Tag>大小 {humanSize(data?.summary.total_size)}</Tag>
          </Space>
          <Space.Compact className="path-input">
            <Input
              prefix={<SearchOutlined />}
              placeholder="输入目录，例如 Plugins 或 Tool/CVWindowsService"
              value={draftPath}
              onChange={(event) => setDraftPath(event.target.value)}
              onPressEnter={() => load(draftPath)}
            />
            <Button type="primary" onClick={() => load(draftPath)}>打开</Button>
            <Button onClick={() => load('')}>根目录</Button>
            {data?.parent_subpath !== undefined && subpath && <Button onClick={() => load(data.parent_subpath)}>上级</Button>}
          </Space.Compact>
        </Space>
      </Card>
      <Card title={`${data?.items.length || 0} 个项目`}>
        <Table rowKey="relative_path" loading={loading} columns={columns} dataSource={data?.items || []} />
      </Card>
    </Space>
  )
}
