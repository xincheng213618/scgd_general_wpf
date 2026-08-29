import {
  AppstoreOutlined,
  BarChartOutlined,
  CloudDownloadOutlined,
  DatabaseOutlined,
  ReloadOutlined,
  SafetyCertificateOutlined,
  TeamOutlined,
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
  listUsers,
} from '../services/admin'
import type {
  AdminStats,
  CacheStatus,
  DeploymentHistoryResponse,
  DocsStatus,
  IndexStatusResponse,
  TrafficStatsResponse,
  UserAccountSummary,
} from '../types/admin'
import type { AuthSession } from '../types/site'
import {
  summarizeDashboardDeployment,
  summarizeDashboardAccountTasks,
  summarizeDashboardIndexes,
  summarizeDashboardTraffic,
  type DashboardHealthLevel,
} from '../utils/dashboardOverview'
import { humanSize, shortDate } from '../utils/format'
import { canOpenAdminRoute, getAdminDashboardCapabilities } from '../utils/permissions'

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

interface DashboardProps {
  session: AuthSession | null
}

export function Dashboard({ session }: DashboardProps) {
  const [stats, setStats] = useState<AdminStats | null>(null)
  const [cache, setCache] = useState<CacheStatus | null>(null)
  const [docs, setDocs] = useState<DocsStatus | null>(null)
  const [traffic, setTraffic] = useState<TrafficStatsResponse | null>(null)
  const [indexes, setIndexes] = useState<IndexStatusResponse | null>(null)
  const [deployments, setDeployments] = useState<DeploymentHistoryResponse | null>(null)
  const [userSummary, setUserSummary] = useState<UserAccountSummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadErrors, setLoadErrors] = useState<string[]>([])
  const [reloadKey, setReloadKey] = useState(0)
  const { readCache, readDeployments, readStats, readUsers } = getAdminDashboardCapabilities(session)
  const hasDashboardDataAccess = readCache || readDeployments || readStats || readUsers

  useEffect(() => {
    let mounted = true
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setLoadErrors([])
      if (!readStats) {
        setStats(null)
        setTraffic(null)
      }
      if (!readCache) {
        setCache(null)
        setDocs(null)
        setIndexes(null)
      }
      if (!readDeployments) setDeployments(null)
      if (!readUsers) setUserSummary(null)
    })

    const requests: Array<{ label: string, promise: Promise<void> }> = []
    if (readStats) {
      requests.push(
        {
          label: '发布统计',
          promise: getAdminStats().then((value) => {
            if (mounted) setStats(value)
          }),
        },
        {
          label: '访问健康',
          promise: getTrafficStats(1, 3).then((value) => {
            if (mounted) setTraffic(value)
          }),
        },
      )
    }
    if (readCache) {
      requests.push(
        {
          label: '缓存状态',
          promise: getCacheStatus().then((value) => {
            if (mounted) setCache(value)
          }),
        },
        {
          label: '文档状态',
          promise: getDocsStatus().then((value) => {
            if (mounted) setDocs(value)
          }),
        },
        {
          label: '索引状态',
          promise: getIndexStatus().then((value) => {
            if (mounted) setIndexes(value)
          }),
        },
      )
    }
    if (readDeployments) {
      requests.push({
        label: '部署历史',
        promise: getDeploymentHistory({ current: 1, pageSize: 1 }).then((value) => {
          if (mounted) setDeployments(value)
        }),
      })
    }
    if (readUsers) {
      requests.push({
        label: '账号安全',
        promise: listUsers({ current: 1, pageSize: 1 }).then((value) => {
          if (mounted) setUserSummary(value.summary)
        }),
      })
    }

    Promise.allSettled(requests.map((item) => item.promise))
      .then((results) => {
        if (!mounted) return
        setLoadErrors(results.map((result, index) => (
          requestError(requests[index]?.label || '总览数据', result)
        )).filter(Boolean))
      })
      .finally(() => {
        if (mounted) setLoading(false)
      })

    return () => {
      mounted = false
    }
  }, [readCache, readDeployments, readStats, readUsers, reloadKey])

  const indexSummary = summarizeDashboardIndexes(indexes)
  const trafficSummary = summarizeDashboardTraffic(traffic)
  const latestDeployment = deployments?.entries[0]
  const deploymentSummary = summarizeDashboardDeployment(latestDeployment)
  const accountTaskSummary = summarizeDashboardAccountTasks(userSummary)
  const docsHealth = docs?.healthStatus || (docs?.built ? 'ok' : 'warning')
  const docsAlertType = docsHealth === 'error' ? 'error' : docsHealth === 'warning' ? 'warning' : 'success'
  const healthCardCount = Number(readStats) + Number(readCache) + Number(readDeployments)
  const healthCardSpan = healthCardCount <= 1 ? 24 : healthCardCount === 2 ? 12 : 8
  const canOpenPublish = canOpenAdminRoute(session, '/admin/publish')
  const canOpenCache = canOpenAdminRoute(session, '/admin/cache')
  const canOpenJobs = canOpenAdminRoute(session, '/admin/jobs')
  const canOpenTraffic = canOpenAdminRoute(session, '/admin/traffic')

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
          <Title level={2}>管理与运维总览</Title>
          <Text type="secondary">
            {hasDashboardDataAccess
              ? '先确认已授权模块的运行健康，再进入对应页面执行操作。'
              : '你已进入管理后台，可以从左侧菜单访问当前角色获准使用的功能。'}
          </Text>
          <Space wrap className="dashboard-shortcuts">
            {canOpenPublish && <Button type="primary" href="/admin/publish">发布中心</Button>}
            {canOpenCache && <Button href="/admin/cache">缓存与索引</Button>}
            {canOpenJobs && <Button href="/admin/jobs">任务调度</Button>}
            {canOpenTraffic && <Button href="/admin/traffic">访问统计</Button>}
            {readUsers && <Button href="/admin/users">账号管理</Button>}
            {hasDashboardDataAccess && (
              <Button
                icon={<ReloadOutlined />}
                loading={loading}
                onClick={() => setReloadKey((key) => key + 1)}
              >
                刷新数据
              </Button>
            )}
          </Space>
        </Space>
      </Card>

      {!hasDashboardDataAccess && (
        <Alert
          type="info"
          showIcon
          message="当前角色没有总览数据权限"
          description="这里不会请求未授权的数据；你仍可通过左侧菜单使用其他已授权功能。"
        />
      )}

      {readUsers && (
        <Card
          title="账号安全待办"
          loading={loading && !userSummary}
          extra={<Button type="link" href="/admin/users">进入账号管理</Button>}
        >
          <Space direction="vertical" size={14} className="wide-space">
            <Space wrap>
              <Tag color={healthColors[accountTaskSummary.level]}>
                {accountTaskSummary.label}
              </Tag>
              <Text type="secondary">{accountTaskSummary.detail}</Text>
              <Tag icon={<TeamOutlined />}>账号 {userSummary?.total ?? 0}</Tag>
              <Tag color="green">启用 {userSummary?.active ?? 0}</Tag>
            </Space>
            {accountTaskSummary.pending > 0 && (
              <Alert
                type="warning"
                showIcon
                icon={<SafetyCertificateOutlined />}
                message="账号安全事项需要处理"
                description="密码找回申请应优先核验并重置临时密码；待改密账号仍需用户本人完成首次密码更新。"
                action={(
                  <Space wrap>
                    {accountTaskSummary.passwordRecoveries > 0 && (
                      <Button type="primary" danger href="/admin/users?recovery_state=pending">
                        处理找回申请 ({accountTaskSummary.passwordRecoveries})
                      </Button>
                    )}
                    {accountTaskSummary.passwordChanges > 0 && (
                      <Button href="/admin/users?password_state=pending">
                        查看待改密账号 ({accountTaskSummary.passwordChanges})
                      </Button>
                    )}
                  </Space>
                )}
              />
            )}
          </Space>
        </Card>
      )}

      {readStats && (
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
      )}

      {hasDashboardDataAccess && (
        <Row gutter={[16, 16]}>
        {readStats && (
          <Col xs={24} xl={healthCardSpan}>
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
        )}

        {readCache && (
          <Col xs={24} xl={healthCardSpan}>
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
        )}

        {readDeployments && (
          <Col xs={24} xl={healthCardSpan}>
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
        )}
        </Row>
      )}

      {readCache && (
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
          {readStats && <Descriptions.Item label="数据库大小">{humanSize(stats?.dbSizeBytes)}</Descriptions.Item>}
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
      )}
    </Space>
  )
}
