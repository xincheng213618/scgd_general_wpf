import {
  AppstoreOutlined,
  BarChartOutlined,
  CloudDownloadOutlined,
  DatabaseOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import { Alert, Badge, Button, Card, Col, Descriptions, Row, Space, Statistic, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import {
  getAdminStats,
  getCacheStatus,
  getDeploymentHistory,
  getDocsStatus,
  getIndexStatus,
  getTrafficStats,
} from '../services/admin'
import type {
  AdminStats,
  CacheStatus,
  DeploymentHistoryResponse,
  DocsStatus,
  IndexStatusResponse,
  TrafficStatsResponse,
} from '../types/admin'
import {
  summarizeDashboardDeployment,
  summarizeDashboardIndexes,
  summarizeDashboardTraffic,
  type DashboardHealthLevel,
} from '../utils/dashboardOverview'
import { humanSize, shortDate } from '../utils/format'

const { Text, Title } = Typography

const healthColors: Record<DashboardHealthLevel, string> = {
  ok: 'green',
  warning: 'gold',
  error: 'red',
  unknown: 'default',
}

function requestError(label: string, result: PromiseSettledResult<unknown>) {
  if (result.status === 'fulfilled') return ''
  const detail = result.reason instanceof Error ? result.reason.message : '请求失败'
  return `${label}：${detail}`
}

export function Dashboard() {
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [cache, setCache] = useState<CacheStatus | null>(null)
  const [docs, setDocs] = useState<DocsStatus | null>(null)
  const [traffic, setTraffic] = useState<TrafficStatsResponse | null>(null)
  const [indexes, setIndexes] = useState<IndexStatusResponse | null>(null)
  const [deployments, setDeployments] = useState<DeploymentHistoryResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadErrors, setLoadErrors] = useState<string[]>([])
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let mounted = true
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setLoadErrors([])
    })

    Promise.allSettled([
      getAdminStats(),
      getCacheStatus(),
      getDocsStatus(),
      getTrafficStats(1, 3),
      getIndexStatus(),
      getDeploymentHistory({ current: 1, pageSize: 1 }),
    ])
      .then(([statsResult, cacheResult, docsResult, trafficResult, indexResult, deploymentResult]) => {
        if (!mounted) return
        if (statsResult.status === 'fulfilled') setStats(statsResult.value)
        if (cacheResult.status === 'fulfilled') setCache(cacheResult.value)
        if (docsResult.status === 'fulfilled') setDocs(docsResult.value)
        if (trafficResult.status === 'fulfilled') setTraffic(trafficResult.value)
        if (indexResult.status === 'fulfilled') setIndexes(indexResult.value)
        if (deploymentResult.status === 'fulfilled') setDeployments(deploymentResult.value)
        setLoadErrors([
          requestError('发布统计', statsResult),
          requestError('缓存状态', cacheResult),
          requestError('文档状态', docsResult),
          requestError('访问健康', trafficResult),
          requestError('索引状态', indexResult),
          requestError('部署历史', deploymentResult),
        ].filter(Boolean))
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })

    return () => {
      mounted = false
    }
  }, [reloadKey])

  const indexSummary = summarizeDashboardIndexes(indexes)
  const trafficSummary = summarizeDashboardTraffic(traffic)
  const latestDeployment = deployments?.entries[0]
  const deploymentSummary = summarizeDashboardDeployment(latestDeployment)
  const docsHealth = docs?.healthStatus || (docs?.built ? 'ok' : 'warning')
  const docsAlertType = docsHealth === 'error' ? 'error' : docsHealth === 'warning' ? 'warning' : 'success'

  return (
    <Space direction="vertical" size={16} className="page-stack">
      {loadErrors.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message="部分总览数据加载失败"
          description={loadErrors.join('；')}
          action={<Button size="small" onClick={() => setReloadKey((key) => key + 1)}>重试</Button>}
        />
      )}

      <Card className="hero-card">
        <Space direction="vertical" size={12} className="wide-space">
          <Tag color="blue">Web Admin</Tag>
          <Title level={2}>发布与运维总览</Title>
          <Text type="secondary">
            先确认访问、索引和部署健康，再进入对应页面执行操作。
          </Text>
          <Space wrap className="dashboard-shortcuts">
            <Button type="primary" href="/admin/publish">发布中心</Button>
            <Button href="/admin/cache">缓存与索引</Button>
            <Button href="/admin/jobs">任务调度</Button>
            <Button href="/admin/traffic">访问统计</Button>
            <Button
              icon={<ReloadOutlined />}
              loading={loading}
              onClick={() => setReloadKey((key) => key + 1)}
            >
              刷新数据
            </Button>
          </Space>
        </Space>
      </Card>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} xl={6}>
          <Card loading={loading && !stats}>
            <Statistic title="插件数量" value={stats?.pluginCount ?? 0} prefix={<AppstoreOutlined />} />
          </Card>
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <Card loading={loading && !stats}>
            <Statistic title="包索引" value={stats?.packageCount ?? 0} prefix={<DatabaseOutlined />} />
          </Card>
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <Card loading={loading && !stats}>
            <Statistic title="今日下载" value={stats?.downloadsToday ?? 0} prefix={<CloudDownloadOutlined />} />
          </Card>
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <Card loading={loading && !stats}>
            <Statistic title="最新版本" value={stats?.latestReleaseVersion || '未检测到'} />
          </Card>
        </Col>
      </Row>

      <Row gutter={[16, 16]}>
        <Col xs={24} xl={8}>
          <Card
            title="访问健康"
            loading={loading && !traffic}
            extra={<Button type="link" href="/admin/traffic">查看详情</Button>}
          >
            <Space direction="vertical" size={10} className="wide-space">
              <Space wrap>
                <Tag color={healthColors[trafficSummary.level]}>{trafficSummary.label}</Tag>
                <Text type="secondary">{trafficSummary.detail}</Text>
              </Space>
              <Statistic title="今日请求" value={traffic?.today.visits ?? stats?.visitsToday ?? 0} prefix={<BarChartOutlined />} />
              <Space wrap>
                <Tag>平均响应 {Math.round(traffic?.today.avgResponseMs ?? stats?.avgResponseMsToday ?? 0)} ms</Tag>
                <Tag color={(traffic?.today.clientErrorResponses || 0) > 0 ? 'gold' : 'default'}>
                  4xx {traffic?.today.clientErrorResponses ?? 0}
                </Tag>
                <Tag color={(traffic?.today.serverErrorResponses || 0) > 0 ? 'red' : 'green'}>
                  5xx {traffic?.today.serverErrorResponses ?? 0}
                </Tag>
              </Space>
            </Space>
          </Card>
        </Col>

        <Col xs={24} xl={8}>
          <Card
            title="索引健康"
            loading={loading && !indexes}
            extra={<Button type="link" href="/admin/cache">进入运维</Button>}
          >
            <Space direction="vertical" size={10} className="wide-space">
              <Space wrap>
                <Tag color={healthColors[indexSummary.level]}>{indexSummary.label}</Tag>
                <Text type="secondary">{indexSummary.detail}</Text>
              </Space>
              <Statistic title="已就绪" value={indexSummary.ready} suffix={`/ ${indexSummary.total}`} prefix={<DatabaseOutlined />} />
              <Text type="secondary">
                索引刷新和数据库备份只在“缓存与索引”页执行。
              </Text>
            </Space>
          </Card>
        </Col>

        <Col xs={24} xl={8}>
          <Card
            title="最近部署"
            loading={loading && !deployments}
            extra={<Button type="link" href="/admin/deployments">查看历史</Button>}
          >
            <Space direction="vertical" size={10} className="wide-space">
              <Space wrap>
                <Tag color={healthColors[deploymentSummary.level]}>{deploymentSummary.label}</Tag>
                {latestDeployment?.source && <Tag>{latestDeployment.source}</Tag>}
              </Space>
              <div className="status-value">
                <Typography.Text code>{latestDeployment?.commit?.slice(0, 10) || '-'}</Typography.Text>
              </div>
              <Text type="secondary">{deploymentSummary.detail}</Text>
              <Text type="secondary">
                {latestDeployment?.timestamp ? shortDate(latestDeployment.timestamp) : '尚无部署时间'}
              </Text>
            </Space>
          </Card>
        </Col>
      </Row>

      <Card
        title="存储与文档"
        loading={loading && !cache && !docs}
        extra={(
          <Space wrap>
            <Button href="/admin/cache">索引运维</Button>
            <Button href="/docs">打开文档</Button>
          </Space>
        )}
      >
        {docs && docsHealth !== 'ok' && (
          <Alert
            type={docsAlertType}
            showIcon
            message={docs.healthMessage || '文档中心需要检查'}
            description={docs.actionHint || '刷新索引或重新构建文档站后再打开文档中心。'}
            className="admin-doc-alert"
          />
        )}
        <Descriptions column={{ xs: 1, md: 2, xl: 3 }} styles={{ content: { minWidth: 0 } }}>
          <Descriptions.Item label="插件目录">
            <Badge status={cache?.plugins_dir_exists ? 'success' : 'warning'} text={cache?.plugins_dir_exists ? '可用' : '待创建'} />
          </Descriptions.Item>
          <Descriptions.Item label="缓存条目">{cache?.cache_entry_count ?? 0}</Descriptions.Item>
          <Descriptions.Item label="过期缓存">{cache?.expired_cache_entry_count ?? 0}</Descriptions.Item>
          <Descriptions.Item label="数据库大小">{humanSize(stats?.dbSizeBytes)}</Descriptions.Item>
          <Descriptions.Item label="Markdown 文档">{docs?.sourceDocumentCount ?? 0}</Descriptions.Item>
          <Descriptions.Item label="文档索引">{docs?.indexedDocumentCount ?? 0}</Descriptions.Item>
          <Descriptions.Item label="文档站">
            <Badge status={docs?.built ? 'success' : 'warning'} text={docs?.built ? '已构建' : '待构建'} />
          </Descriptions.Item>
          <Descriptions.Item label="文档搜索">
            <Tag color={docs?.searchIndexExists ? 'green' : 'default'}>{docs?.searchIndexExists ? '可用' : '未生成'}</Tag>
          </Descriptions.Item>
          <Descriptions.Item label="索引更新时间">
            {docs?.indexUpdatedAt ? shortDate(docs.indexUpdatedAt) : '-'}
          </Descriptions.Item>
          <Descriptions.Item label="存储路径" span={3}>
            <div className="mono-line">{cache?.storage_path || '-'}</div>
          </Descriptions.Item>
        </Descriptions>
      </Card>
    </Space>
  )
}
