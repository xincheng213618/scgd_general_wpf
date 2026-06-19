import {
  AppstoreOutlined,
  CloudDownloadOutlined,
  DatabaseOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import { ProDescriptions } from '@ant-design/pro-components'
import { App, Badge, Button, Card, Col, Popconfirm, Row, Space, Statistic, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { cleanupCache, getAdminStats, getCacheStatus, refreshAllIndexes } from '../services/admin'
import type { AdminStats, CacheStatus } from '../types/admin'

const { Text, Title } = Typography

export function Dashboard() {
  const { message } = App.useApp()
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [cache, setCache] = useState<CacheStatus | null>(null)
  const [loading, setLoading] = useState(true)

  const load = async () => {
    setLoading(true)
    try {
      const [nextStats, nextCache] = await Promise.all([getAdminStats(), getCacheStatus()])
      setStats(nextStats)
      setCache(nextCache)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '加载控制台失败')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let mounted = true
    Promise.all([getAdminStats(), getCacheStatus()])
      .then(([nextStats, nextCache]) => {
        if (!mounted) return
        setStats(nextStats)
        setCache(nextCache)
      })
      .catch((error) => {
        if (mounted) {
          message.error(error instanceof Error ? error.message : '加载控制台失败')
        }
      })
      .finally(() => {
        if (mounted) {
          setLoading(false)
        }
      })
    return () => {
      mounted = false
    }
  }, [message])

  const refreshIndexes = async () => {
    await refreshAllIndexes()
    message.success('索引刷新已完成')
    await load()
  }

  const cleanCache = async () => {
    const result = await cleanupCache()
    message.success(`已清理 ${result.deleted_count} 条过期缓存`)
    await load()
  }

  return (
    <Space direction="vertical" size={16} className="page-stack">
      <Card loading={loading} className="hero-card">
        <Space direction="vertical" size={12}>
          <Tag color="blue">Web Admin</Tag>
          <Title level={2}>发布与运维控制台</Title>
          <Text type="secondary">
            管理安装包、插件、增量包、缓存索引、任务调度、API Key 与审计日志。
          </Text>
          <Space wrap>
            <Button type="primary" icon={<ReloadOutlined />} onClick={refreshIndexes}>
              刷新全部索引
            </Button>
            <Popconfirm title="确认清理过期缓存？" onConfirm={cleanCache}>
              <Button danger>清理过期缓存</Button>
            </Popconfirm>
            <Button href="/">前台发布站</Button>
          </Space>
        </Space>
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="插件数量" value={stats?.pluginCount ?? 0} prefix={<AppstoreOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="包索引" value={stats?.packageCount ?? 0} prefix={<DatabaseOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="今日下载" value={stats?.downloadsToday ?? 0} prefix={<CloudDownloadOutlined />} />
          </Card>
        </Col>
        <Col xs={24} md={12} xl={6}>
          <Card loading={loading}>
            <Statistic title="总下载" value={stats?.totalDownloads ?? 0} prefix={<CloudDownloadOutlined />} />
          </Card>
        </Col>
      </Row>

      <Card title="系统详情" loading={loading}>
        <ProDescriptions column={{ xs: 1, md: 2, xl: 3 }} dataSource={{ ...stats, ...cache }}>
          <ProDescriptions.Item label="最新版本" dataIndex="latestReleaseVersion">
            {stats?.latestReleaseVersion || '未检测到'}
          </ProDescriptions.Item>
          <ProDescriptions.Item label="插件目录">
            <Badge status={cache?.plugins_dir_exists ? 'success' : 'warning'} text={cache?.plugins_dir_exists ? '可用' : '待创建'} />
          </ProDescriptions.Item>
          <ProDescriptions.Item label="插件目录缓存">
            <Tag color={stats?.pluginCatalogCached ? 'green' : 'default'}>
              {stats?.pluginCatalogCached ? '已缓存' : '未缓存'}
            </Tag>
          </ProDescriptions.Item>
          <ProDescriptions.Item label="缓存条目">{cache?.cache_entry_count ?? 0}</ProDescriptions.Item>
          <ProDescriptions.Item label="过期缓存">{cache?.expired_cache_entry_count ?? 0}</ProDescriptions.Item>
          <ProDescriptions.Item label="存储路径" span={3}>
            <Typography.Text code copyable>
              {cache?.storage_path || '-'}
            </Typography.Text>
          </ProDescriptions.Item>
        </ProDescriptions>
      </Card>
    </Space>
  )
}
