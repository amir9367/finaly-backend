import { useCallback, useEffect, useState } from 'react'
import { Button, Card, Table, Tag, Tooltip, message } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import { api, errMsg } from '../api'
import { SMS_TYPE_FA, type SmsLogDto } from '../types'

const STATUS_COLOR: Record<string, string> = {
  Pending: 'blue',
  Sent: 'green',
  Failed: 'red',
}

export default function SmsLogs() {
  const [logs, setLogs] = useState<SmsLogDto[]>([])
  const [loading, setLoading] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const response = await api.get<SmsLogDto[]>('/admin/sms/logs', { params: { take: 200 } })
      setLogs(response.data)
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <div className="page-card">
      <Card
        title="گزارش پیامک‌ها"
        extra={
          <Button icon={<ReloadOutlined />} onClick={load}>
            بازخوانی
          </Button>
        }
      >
        <Table
          rowKey="id"
          loading={loading}
          dataSource={logs}
          scroll={{ x: 800 }}
          pagination={{ pageSize: 15 }}
          columns={[
            {
              title: 'زمان',
              dataIndex: 'createdAt',
              key: 'time',
              width: 170,
              render: (value: string) => new Date(value).toLocaleString('fa-IR'),
            },
            { title: 'موبایل', dataIndex: 'phone', key: 'phone', width: 130 },
            {
              title: 'نوع',
              dataIndex: 'type',
              key: 'type',
              width: 120,
              render: (value: string) => SMS_TYPE_FA[value] ?? value,
            },
            {
              title: 'متن',
              dataIndex: 'body',
              key: 'body',
              render: (value: string) => (
                <Tooltip title={value}>
                  <span>{value}</span>
                </Tooltip>
              ),
            },
            {
              title: 'وضعیت',
              dataIndex: 'status',
              key: 'status',
              width: 100,
              render: (value: string) => <Tag color={STATUS_COLOR[value] ?? 'default'}>{value}</Tag>,
            },
            {
              title: 'خطا',
              dataIndex: 'error',
              key: 'error',
              render: (value: string | null) =>
                value ? <span style={{ color: '#cf1322' }}>{value}</span> : <span>-</span>,
            },
          ]}
        />
      </Card>
    </div>
  )
}
