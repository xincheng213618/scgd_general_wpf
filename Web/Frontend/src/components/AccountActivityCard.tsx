import { HistoryOutlined, ReloadOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import { Alert, Button, Card, List, Pagination, Space, Spin, Tag, Typography } from 'antd'
import { useEffect, useState } from 'react'
import { getAccountActivity } from '../services/auth'
import type { AccountActivityEntry, AccountActivityResponse } from '../types/site'
import { accountActivitySourceLabel } from '../utils/accountActivity'
import { sessionAddressLabel, sessionClientLabel } from '../utils/accountSessions'
import { auditActionMeta, auditDetailSummary } from '../utils/auditLog'
import { shortDate } from '../utils/format'

const PAGE_SIZE = 8

function ActivityItem({ item }: { item: AccountActivityEntry }) {
  const meta = auditActionMeta(item.action)
  return (
    <List.Item>
      <List.Item.Meta
        avatar={<SafetyCertificateOutlined style={{ color: item.security ? '#cf1322' : '#1677ff', fontSize: 22 }} />}
        title={(
          <Space size={[6, 6]} wrap>
            <Tag color={meta.color}>{meta.label}</Tag>
            <Tag color={item.source === 'anonymous' ? 'volcano' : 'default'}>
              {accountActivitySourceLabel(item.source)}
            </Tag>
            <Typography.Text type="secondary">{shortDate(item.created_at)}</Typography.Text>
          </Space>
        )}
        description={(
          <Space direction="vertical" size={2} style={{ width: '100%' }}>
            <Typography.Text>{auditDetailSummary(item.detail)}</Typography.Text>
            <Typography.Text type="secondary">
              {sessionAddressLabel(item.ip)} · {sessionClientLabel(item.user_agent)}
            </Typography.Text>
          </Space>
        )}
      />
    </List.Item>
  )
}

export function AccountActivityCard() {
  const [page, setPage] = useState(1)
  const [reloadToken, setReloadToken] = useState(0)
  const [result, setResult] = useState<AccountActivityResponse | null>(null)
  const [loadError, setLoadError] = useState('')
  const [refreshing, setRefreshing] = useState(false)

  useEffect(() => {
    let active = true
    getAccountActivity({ current: page, pageSize: PAGE_SIZE })
      .then((value) => {
        if (active) {
          setResult(value)
          setLoadError('')
        }
      })
      .catch((error) => {
        if (active) {
          setLoadError(error instanceof Error ? error.message : '账号活动加载失败')
        }
      })
      .finally(() => {
        if (active) setRefreshing(false)
      })
    return () => {
      active = false
    }
  }, [page, reloadToken])

  return (
    <Card
      title={<Space><HistoryOutlined />账号活动</Space>}
      extra={(
        <Space>
          {result && result.summary.security_events > 0 && (
            <Tag color="red">安全事件 {result.summary.security_events}</Tag>
          )}
          <Button
            size="small"
            icon={<ReloadOutlined aria-hidden="true" />}
            loading={refreshing}
            disabled={refreshing || result === null || Boolean(loadError)}
            onClick={() => {
              setLoadError('')
              setRefreshing(true)
              setReloadToken((value) => value + 1)
            }}
          >
            刷新
          </Button>
        </Space>
      )}
    >
      {loadError ? (
        <Alert
          type="error"
          showIcon
          message="账号活动加载失败"
          description={loadError}
          action={(
            <Button
              size="small"
              onClick={() => {
                setLoadError('')
                setResult(null)
                setReloadToken((value) => value + 1)
              }}
            >
              重试
            </Button>
          )}
        />
      ) : result === null ? (
        <Spin tip="加载账号活动…" />
      ) : (
        <Space direction="vertical" size={16} style={{ width: '100%' }}>
          {(result.summary.failed_logins > 0 || result.summary.throttled_logins > 0) && (
            <Alert
              type="warning"
              showIcon
              message={result.summary.throttled_logins > 0
                ? `活动记录中有 ${result.summary.failed_logins} 次失败登录，触发 ${result.summary.throttled_logins} 次临时锁定`
                : `活动记录中有 ${result.summary.failed_logins} 次失败登录`}
              description="如非本人操作，建议立即修改密码，并在登录设备中退出其他会话。"
            />
          )}
          <List<AccountActivityEntry>
            dataSource={result.entries}
            locale={{ emptyText: '暂无账号活动' }}
            renderItem={(item) => <ActivityItem item={item} />}
          />
          {result.total > PAGE_SIZE && (
            <Pagination
              current={page}
              pageSize={PAGE_SIZE}
              total={result.total}
              showSizeChanger={false}
              showTotal={(total) => `共 ${total} 条活动`}
              onChange={(nextPage) => {
                setResult(null)
                setLoadError('')
                setPage(nextPage)
              }}
            />
          )}
        </Space>
      )}
    </Card>
  )
}
