import { DatabaseOutlined, ReloadOutlined } from '@ant-design/icons'
import { ProTable, type ActionType, type ProColumns } from '@ant-design/pro-components'
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Empty,
  List,
  Popconfirm,
  Row,
  Space,
  Statistic,
  Tag,
  Typography,
} from 'antd'
import { useRef, useState } from 'react'
import {
  backupDatabase,
  cleanupCache,
  getCacheStatus,
  getIndexStatus,
  listDatabaseBackups,
  refreshAllIndexes,
  refreshIndex,
} from '../services/admin'
import type {
  CacheStatus,
  DatabaseBackupInventory,
  IndexRefreshResult,
  IndexScope,
  IndexStatusRow,
} from '../types/admin'
import { humanSize, shortDate } from '../utils/format'
import { buildIndexStatusRows, indexPanelHealth } from '../utils/indexMaintenance'

const indexStatusMeta: Record<string, { label: string; color: string }> = {
  ready: { label: '就绪', color: 'green' },
  refreshing: { label: '刷新中', color: 'processing' },
  error: { label: '异常', color: 'red' },
  not_initialized: { label: '未初始化', color: 'gold' },
}

function refreshErrorCount(result: Record<string, IndexRefreshResult>) {
  return Object.values(result).reduce((total, item) => total + (item.errors?.length || 0), 0)
}

export function CachePage() {
  const { message, modal } = App.useApp()
  const actionRef = useRef<ActionType>(null)
  const [cache, setCache] = useState<CacheStatus | null>(null)
  const [backups, setBackups] = useState<DatabaseBackupInventory>({ backups: [], count: 0, keep_count: 10 })
  const [rows, setRows] = useState<IndexStatusRow[]>([])
  const [loadError, setLoadError] = useState('')
  const [refreshing, setRefreshing] = useState<IndexScope | 'all' | null>(null)
  const [cleaning, setCleaning] = useState(false)
  const [backingUp, setBackingUp] = useState(false)

  const reload = () => actionRef.current?.reload()

  const runRefresh = async (scope?: IndexScope) => {
    const target = scope || 'all'
    setRefreshing(target)
    try {
      if (scope) {
        const result = await refreshIndex(scope)
        const errors = result.errors?.length || 0
        const name = rows.find((row) => row.scope === scope)?.name || scope
        if (errors) message.warning(`${name}索引刷新完成，但有 ${errors} 个错误`)
        else message.success(`${name}索引刷新已完成`)
      } else {
        const result = await refreshAllIndexes()
        const errors = refreshErrorCount(result)
        if (errors) message.warning(`全部索引刷新完成，但有 ${errors} 个错误`)
        else message.success('全部索引刷新已完成')
      }
      reload()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '索引刷新失败')
    } finally {
      setRefreshing(null)
    }
  }

  const cleanExpiredCache = async () => {
    setCleaning(true)
    try {
      const result = await cleanupCache()
      message.success(`已清理 ${result.deleted_count} 条过期缓存`)
      reload()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '缓存清理失败')
    } finally {
      setCleaning(false)
    }
  }

  const createBackup = async () => {
    setBackingUp(true)
    try {
      const result = await backupDatabase()
      modal.success({
        title: '数据库备份已创建',
        content: (
          <Space direction="vertical" size={4}>
            <Typography.Text code>{result.backup_name}</Typography.Text>
            <Typography.Text type="secondary">
              {humanSize(result.backup_size_bytes)} · 当前保留 {result.backup_retention.afterCount} 个
              {result.backup_retention.removedCount > 0
                ? ` · 已轮换 ${result.backup_retention.removedCount} 个旧备份`
                : ''}
            </Typography.Text>
          </Space>
        ),
      })
      reload()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '数据库备份失败')
    } finally {
      setBackingUp(false)
    }
  }

  const columns: ProColumns<IndexStatusRow>[] = [
    {
      title: '索引',
      dataIndex: 'name',
      width: 140,
    },
    {
      title: '状态',
      dataIndex: 'status',
      width: 110,
      render: (_, record) => {
        const meta = indexStatusMeta[record.status] || { label: record.status, color: 'default' }
        return <Tag color={meta.color}>{meta.label}</Tag>
      },
    },
    {
      title: '有效条目',
      dataIndex: 'indexed_count',
      width: 110,
      align: 'right',
      render: (_, record) => record.indexed_count.toLocaleString(),
    },
    {
      title: '最近完成',
      dataIndex: 'last_finished_at',
      width: 175,
      render: (_, record) => record.last_finished_at ? shortDate(record.last_finished_at) : '从未完成',
    },
    {
      title: '耗时',
      dataIndex: 'duration_ms',
      width: 100,
      align: 'right',
      render: (_, record) => record.duration_ms ? `${record.duration_ms.toLocaleString()} ms` : '-',
    },
    {
      title: '最近错误',
      dataIndex: 'last_error',
      ellipsis: true,
      render: (_, record) => record.last_error
        ? <Typography.Text type="danger">{record.last_error}</Typography.Text>
        : <Typography.Text type="secondary">无</Typography.Text>,
    },
    {
      title: '操作',
      valueType: 'option',
      width: 100,
      render: (_, record) => (
        <Button
          size="small"
          icon={<ReloadOutlined />}
          loading={refreshing === record.scope}
          disabled={refreshing !== null && refreshing !== record.scope}
          onClick={() => runRefresh(record.scope)}
        >
          刷新
        </Button>
      ),
    },
  ]

  const health = indexPanelHealth(rows)
  const problemRows = rows.filter((row) => row.status !== 'ready' || row.last_error)

  return (
    <Space direction="vertical" size={16} className="page-stack">
      {loadError && (
        <Alert
          type="error"
          showIcon
          message="运维状态加载失败"
          description={loadError}
          action={<Button size="small" onClick={reload}>重试</Button>}
        />
      )}
      {!loadError && health !== 'ok' && rows.length > 0 && (
        <Alert
          type={health === 'error' ? 'error' : 'warning'}
          showIcon
          message={health === 'error' ? '存在索引异常' : '存在未就绪索引'}
          description={problemRows.map((row) => `${row.name}: ${row.last_error || indexStatusMeta[row.status]?.label || row.status}`).join('；')}
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={12} lg={6}>
          <Card><Statistic title="缓存条目" value={cache?.cache_entry_count ?? 0} /></Card>
        </Col>
        <Col xs={12} lg={6}>
          <Card><Statistic title="过期缓存" value={cache?.expired_cache_entry_count ?? 0} valueStyle={{ color: cache?.expired_cache_entry_count ? '#d46b08' : undefined }} /></Card>
        </Col>
        <Col xs={12} lg={6}>
          <Card><Statistic title="插件包索引" value={cache?.package_index_count ?? 0} /></Card>
        </Col>
        <Col xs={12} lg={6}>
          <Card><Statistic title="数据库备份" value={backups.count} suffix={`/ ${backups.keep_count}`} prefix={<DatabaseOutlined />} /></Card>
        </Col>
      </Row>

      <ProTable<IndexStatusRow>
        className="cache-index-table"
        actionRef={actionRef}
        rowKey="scope"
        columns={columns}
        search={false}
        pagination={false}
        request={async () => {
          try {
            const [status, cacheStatus, backupInventory] = await Promise.all([
              getIndexStatus(),
              getCacheStatus(),
              listDatabaseBackups(),
            ])
            const nextRows = buildIndexStatusRows(status)
            setRows(nextRows)
            setCache(cacheStatus)
            setBackups(backupInventory)
            setLoadError(status.error || '')
            return { data: nextRows, success: !status.error, total: nextRows.length }
          } catch (error) {
            setLoadError(error instanceof Error ? error.message : '无法读取运维状态')
            return { data: [], success: false, total: 0 }
          }
        }}
        toolBarRender={() => [
          <Button
            key="refresh"
            type="primary"
            icon={<ReloadOutlined />}
            loading={refreshing === 'all'}
            disabled={refreshing !== null && refreshing !== 'all'}
            onClick={() => runRefresh()}
          >
            刷新全部索引
          </Button>,
          <Popconfirm key="cleanup" title="确认清理过期缓存？" onConfirm={cleanExpiredCache}>
            <Button danger loading={cleaning}>清理过期缓存</Button>
          </Popconfirm>,
          <Popconfirm
            key="backup"
            title="确认创建数据库备份？"
            description={`系统会立即创建一致性快照、执行隐私保留清理，并自动保留最新 ${backups.keep_count} 个备份。`}
            onConfirm={createBackup}
          >
            <Button icon={<DatabaseOutlined />} loading={backingUp}>立即备份数据库</Button>
          </Popconfirm>,
        ]}
        options={{ density: true, fullScreen: true, reload: true, setting: true }}
        cardBordered
        headerTitle="索引运行状态"
        scroll={{ x: 1050 }}
      />

      <Card
        title="数据库备份"
        extra={<Typography.Text type="secondary">每日自动创建 · UTC 时间 · 隐私清理与轮换</Typography.Text>}
      >
        {backups.backups.length ? (
          <List
            dataSource={backups.backups}
            renderItem={(backup) => (
              <List.Item extra={<Typography.Text type="secondary">{humanSize(backup.size_bytes)}</Typography.Text>}>
                <List.Item.Meta
                  title={<Typography.Text code>{backup.name}</Typography.Text>}
                  description={shortDate(backup.created_at)}
                />
              </List.Item>
            )}
          />
        ) : (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="尚无数据库备份；每日任务会自动创建，也可立即备份" />
        )}
      </Card>
    </Space>
  )
}
