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
  getFeedbackInbox,
  getTrafficStats,
  listUsers,
  listJobs,
  listDatabaseBackups,
  getOperationsOverview,
} from '../services/admin'
import type {
  AdminStats,
  CacheStatus,
  DeploymentHistoryResponse,
  DocsStatus,
  IndexStatusResponse,
  TrafficStatsResponse,
  UserAccountSummary,
  FeedbackInboxResponse,
  ScheduledJob,
  DatabaseBackupInventory,
  OperationsOverview,
} from '../types/admin'
import type { AuthSession } from '../types/site'
import {
  summarizeDashboardDeployment,
  summarizeDashboardAccountTasks,
  summarizeDashboardIndexes,
  summarizeDashboardTraffic,
  summarizeDashboardJobs,
  summarizeDashboardBackup,
  type DashboardHealthLevel,
} from '../utils/dashboardOverview'
import { humanSize, shortDate } from '../utils/format'
import { feedbackAgeInfo, feedbackBeijingTime } from '../utils/feedback'
import { jobHistoryPath, jobNeedsAttention } from '../utils/jobOperations'
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
  const [feedbackSummary, setFeedbackSummary] = useState<FeedbackInboxResponse['summary'] | null>(null)
  const [jobs, setJobs] = useState<ScheduledJob[] | null>(null)
  const [backups, setBackups] = useState<DatabaseBackupInventory | null>(null)
  const [operations, setOperations] = useState<OperationsOverview | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadErrors, setLoadErrors] = useState<string[]>([])
  const [reloadKey, setReloadKey] = useState(0)
  const { readCache, readDeployments, readStats, readUsers, readFeedback, readJobs, readBackups, readOperations } = getAdminDashboardCapabilities(session)
  const hasDashboardDataAccess = readCache || readDeployments || readStats || readUsers || readFeedback || readJobs || readBackups || readOperations

  useEffect(() => {
    let mounted = true
    const controller = new AbortController()
    const signal = AbortSignal.any([controller.signal, AbortSignal.timeout(15000)])
    queueMicrotask(() => {
      if (!mounted) return
      setLoading(true)
      setLoadErrors([])
      setStats(null)
      setTraffic(null)
      setCache(null)
      setDocs(null)
      setIndexes(null)
      setDeployments(null)
      setUserSummary(null)
      setFeedbackSummary(null)
      setJobs(null)
      setBackups(null)
      setOperations(null)
    })

    const requests: Array<{ label: string, promise: Promise<void> }> = []
    if (readJobs) requests.push({ label: '任务状态', promise: listJobs(signal).then((value) => { if (mounted) setJobs(value) }) })
    if (readBackups) requests.push({ label: '备份状态', promise: listDatabaseBackups(signal).then((value) => { if (mounted) setBackups(value) }) })
    if (readOperations) requests.push({ label: '终端状态', promise: getOperationsOverview(signal, { hostLimit: 3, activityLimit: 1 }).then((value) => { if (mounted) setOperations(value) }) })
    if (readFeedback) {
      requests.push({
        label: '反馈待办',
        promise: getFeedbackInbox({ status: 'open', pageSize: 1 }, signal).then((value) => {
          if (mounted) setFeedbackSummary(value.summary)
        }),
      })
    }
    if (readStats) {
      requests.push(
        {
          label: '发布统计',
          promise: getAdminStats(signal).then((value) => {
            if (mounted) setStats(value)
          }),
        },
        {
          label: '访问健康',
          promise: getTrafficStats(1, 3, signal).then((value) => {
            if (mounted) setTraffic(value)
          }),
        },
      )
    }
    if (readCache) {
      requests.push(
        {
          label: '缓存状态',
          promise: getCacheStatus(signal).then((value) => {
            if (mounted) setCache(value)
          }),
        },
        {
          label: '文档状态',
          promise: getDocsStatus(signal).then((value) => {
            if (mounted) setDocs(value)
          }),
        },
        {
          label: '索引状态',
          promise: getIndexStatus(signal).then((value) => {
            if (mounted) setIndexes(value)
          }),
        },
      )
    }
    if (readDeployments) {
      requests.push({
        label: '部署历史',
        promise: getDeploymentHistory({ current: 1, pageSize: 1 }, signal).then((value) => {
          if (mounted) setDeployments(value)
        }),
      })
    }
    if (readUsers) {
      requests.push({
        label: '账号安全',
        promise: listUsers({ current: 1, pageSize: 1 }, signal).then((value) => {
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
      controller.abort()
    }
  }, [readCache, readDeployments, readStats, readUsers, readFeedback, readJobs, readBackups, readOperations, reloadKey])

  const indexSummary = summarizeDashboardIndexes(indexes)
  const trafficSummary = summarizeDashboardTraffic(traffic)
  const latestDeployment = deployments?.entries[0]
  const deploymentSummary = summarizeDashboardDeployment(latestDeployment)
  const accountTaskSummary = summarizeDashboardAccountTasks(userSummary)
  const jobSummary = summarizeDashboardJobs(jobs)
  const backupSummary = summarizeDashboardBackup(backups, jobs)
  const backupJob = jobs?.find((job) => job.job_type === 'database_backup')
  const latestBackup = backups?.backups[0]
  const docsHealth = docs?.healthStatus || (docs?.built ? 'ok' : 'warning')
  const docsAlertType = docsHealth === 'error' ? 'error' : docsHealth === 'warning' ? 'warning' : 'success'
  const healthCardCount = Number(readStats) + Number(readCache) + Number(readDeployments)
  const healthCardSpan = healthCardCount <= 1 ? 24 : healthCardCount === 2 ? 12 : 8
  const operationsCardCount = Number(readJobs) + Number(readBackups) + Number(readOperations)
  const operationsCardSpan = operationsCardCount <= 1 ? 24 : operationsCardCount === 2 ? 12 : 8
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

      {(readJobs || readBackups || readOperations) && (
        <Row gutter={[16, 16]}>
          {readJobs && (
            <Col xs={24} xl={operationsCardSpan}>
              <Card title="任务待办" loading={loading && !jobs} extra={<Button type="link" href="/admin/jobs">全部任务</Button>}>
                <Space direction="vertical" size={12} className="wide-space">
                  <Tag color={healthColors[jobSummary.level]}>{jobSummary.label}</Tag>
                  <Text type="secondary">{jobSummary.detail}</Text>
                  {jobs?.filter(jobNeedsAttention).slice(0, 3).map((job) => (
                    <Button className="dashboard-detail-link" key={job.id} href={jobHistoryPath(job.id)}>{job.name} · 查看原因</Button>
                  ))}
                  {(jobs?.filter(jobNeedsAttention).length || 0) > 3 && <Button href="/admin/jobs?view=attention">查看全部异常任务</Button>}
                </Space>
              </Card>
            </Col>
          )}
          {readBackups && (
            <Col xs={24} xl={operationsCardSpan}>
              <Card title="数据库备份" loading={loading && !backups} extra={<Button type="link" href="/admin/cache#database-backups">查看备份</Button>}>
                <Space direction="vertical" size={12} className="wide-space">
                  <Tag color={healthColors[backupSummary.level]}>{backupSummary.label}</Tag>
                  <Text type="secondary">{backupSummary.detail}</Text>
                  <Text>最近文件：{latestBackup ? shortDate(latestBackup.created_at) : backups ? '暂无' : '未知'}</Text>
                  <Text type="secondary">保留文件 {backups?.count ?? '—'} 份</Text>
                  {backupJob && <Button href={jobHistoryPath(backupJob.id)}>查看自动备份执行记录</Button>}
                </Space>
              </Card>
            </Col>
          )}
          {readOperations && (
            <Col xs={24} xl={operationsCardSpan}>
              <Card title="终端连接" loading={loading && !operations} extra={<Button type="link" href="/admin/operations/hosts">查看终端</Button>}>
                <Space direction="vertical" size={12} className="wide-space">
                  {operations ? <>
                    <Space wrap>
                      <Tag color={operations.summary.staleHosts ? 'gold' : operations.summary.totalHosts ? 'green' : 'default'}>
                        {operations.summary.totalHosts ? `${operations.summary.onlineHosts} / ${operations.summary.totalHosts} 台在线` : '尚无已登记终端'}
                      </Tag>
                      {operations.summary.staleHosts > 0 && <Tag color="gold">{operations.summary.staleHosts} 台心跳未更新</Tag>}
                    </Space>
                    <Text type="secondary">超过 {operations.onlineThresholdSeconds} 秒无心跳视为未连接，请结合终端开关机情况检查。</Text>
                    {operations.hosts.map((host) => <Button className="dashboard-detail-link" key={host.hostId} href={`/admin/operations/hosts?host=${encodeURIComponent(host.hostId)}`}>{host.displayName} · {host.online ? '在线' : '未连接'}</Button>)}
                    <Text type="secondary">最近上报的终端 · {shortDate(operations.generatedAt)}</Text>
                  </> : <Text type="secondary">终端状态未知，请刷新重试。</Text>}
                </Space>
              </Card>
            </Col>
          )}
        </Row>
      )}

      {readFeedback && (
        <Card title="反馈待办" loading={loading && !feedbackSummary}
          extra={<Button type="link" href="/admin/feedback?status=open">查看未解决反馈</Button>}>
          {feedbackSummary ? (
            <Space direction="vertical" size={14} className="wide-space">
              <Space wrap>
                <Button href="/admin/feedback?status=open">未解决 {feedbackSummary.status_counts.new + feedbackSummary.status_counts.in_progress}</Button>
                <Button href="/admin/feedback?status=new">待处理 {feedbackSummary.status_counts.new}</Button>
                <Button href="/admin/feedback?status=in_progress">处理中 {feedbackSummary.status_counts.in_progress}</Button>
              </Space>
              <Text type="secondary">
                {feedbackSummary.oldest_open_at
                  ? `最早未解决：${feedbackBeijingTime(feedbackSummary.oldest_open_at)} · ${feedbackAgeInfo('new', feedbackSummary.oldest_open_at).label}`
                  : feedbackSummary.status_counts.new + feedbackSummary.status_counts.in_progress > 0
                    ? '存在未解决反馈，接收时间未知'
                    : '当前没有未解决反馈'}
              </Text>
            </Space>
          ) : <Text type="secondary">反馈摘要暂不可用，请刷新重试。</Text>}
        </Card>
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
              <Statistic title="插件数量" value={stats?.pluginCount ?? '—'} prefix={<AppstoreOutlined />} />
            </Card>
          </Col>
          <Col xs={24} sm={12} xl={6}>
            <Card loading={loading && !stats}>
              <Statistic title="包索引" value={stats?.packageCount ?? '—'} prefix={<DatabaseOutlined />} />
            </Card>
          </Col>
          <Col xs={24} sm={12} xl={6}>
            <Card loading={loading && !stats}>
              <Statistic title="今日下载" value={stats?.downloadsToday ?? '—'} prefix={<CloudDownloadOutlined />} />
            </Card>
          </Col>
          <Col xs={24} sm={12} xl={6}>
            <Card loading={loading && !stats}>
              <Statistic title="最新版本" value={stats ? stats.latestReleaseVersion || '未检测到' : '未知'} />
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
              <Statistic title="今日请求" value={traffic?.today.visits ?? stats?.visitsToday ?? '—'} prefix={<BarChartOutlined />} />
              <Space wrap>
                <Tag>平均响应 {traffic || stats ? Math.round(traffic?.today.avgResponseMs ?? stats?.avgResponseMsToday ?? 0) : '—'} ms</Tag>
                <Tag color={(traffic?.today.clientErrorResponses || 0) > 0 ? 'gold' : 'default'}>
                  4xx {traffic?.today.clientErrorResponses ?? '—'}
                </Tag>
                <Tag color={!traffic ? 'default' : traffic.today.serverErrorResponses > 0 ? 'red' : 'green'}>
                  5xx {traffic?.today.serverErrorResponses ?? '—'}
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
              <Statistic title="已就绪" value={indexes ? indexSummary.ready : '—'} suffix={`/ ${indexSummary.total}`} prefix={<DatabaseOutlined />} />
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
                {latestDeployment?.timestamp ? shortDate(latestDeployment.timestamp) : deployments ? '尚无部署时间' : '部署记录读取未完成'}
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
            <Badge status={!cache ? 'default' : cache.plugins_dir_exists ? 'success' : 'warning'} text={!cache ? '未知' : cache.plugins_dir_exists ? '可用' : '待创建'} />
          </Descriptions.Item>
          <Descriptions.Item label="缓存条目">{cache?.cache_entry_count ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="过期缓存">{cache?.expired_cache_entry_count ?? '—'}</Descriptions.Item>
          {readStats && <Descriptions.Item label="数据库大小">{humanSize(stats?.dbSizeBytes)}</Descriptions.Item>}
          <Descriptions.Item label="Markdown 文档">{docs?.sourceDocumentCount ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="文档索引">{docs?.indexedDocumentCount ?? '—'}</Descriptions.Item>
          <Descriptions.Item label="文档站">
            <Badge status={!docs ? 'default' : docs.built ? 'success' : 'warning'} text={!docs ? '未知' : docs.built ? '已构建' : '待构建'} />
          </Descriptions.Item>
          <Descriptions.Item label="文档搜索">
            <Tag color={docs?.searchIndexExists ? 'green' : 'default'}>{!docs ? '未知' : docs.searchIndexExists ? '可用' : '未生成'}</Tag>
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
