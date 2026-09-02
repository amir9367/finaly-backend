import { useCallback, useEffect, useState } from 'react'
import {
  Button,
  Card,
  Form,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  message,
} from 'antd'
import { DownloadOutlined, ReloadOutlined } from '@ant-design/icons'
import { api, downloadFile, errMsg } from '../api'
import { SOURCE_FA, STATUS_FA, type AppointmentDto, type DoctorAdminDto } from '../types'

const STATUS_COLOR: Record<string, string> = {
  Booked: 'green',
  CancelledByPatient: 'orange',
  CancelledByClinic: 'red',
}

export default function AppointmentsPage() {
  const [appointments, setAppointments] = useState<AppointmentDto[]>([])
  const [doctors, setDoctors] = useState<DoctorAdminDto[]>([])
  const [doctorId, setDoctorId] = useState<string | undefined>()
  const [status, setStatus] = useState<string | undefined>()
  const [loading, setLoading] = useState(false)

  const [cancelTarget, setCancelTarget] = useState<AppointmentDto | null>(null)
  const [cancelForm] = Form.useForm<{ reason?: string }>()
  const [rescheduleTarget, setRescheduleTarget] = useState<AppointmentDto | null>(null)
  const [rescheduleForm] = Form.useForm<{ newStartJalali: string }>()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const response = await api.get<AppointmentDto[]>('/admin/appointments', {
        params: { doctorId, status, take: 300 },
      })
      // Ensure chronological order (earliest first) regardless of filter — for staff convenience
      const sorted = [...response.data].sort(
        (a, b) => new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime()
      )
      setAppointments(sorted)
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setLoading(false)
    }
  }, [doctorId, status])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    api.get<DoctorAdminDto[]>('/admin/doctors')
      .then((response) => setDoctors(response.data))
      .catch(() => undefined)
    // Doctor list failures surface when loading appointments anyway.
  }, [])

  const cancel = async (values: { reason?: string }) => {
    if (!cancelTarget) return
    try {
      await api.patch(`/admin/appointments/${cancelTarget.id}/cancel`, values)
      message.success('نوبت لغو و پیامک اعلام شد')
      setCancelTarget(null)
      cancelForm.resetFields()
      await load()
    } catch (error) {
      message.error(errMsg(error))
    }
  }

  const reschedule = async (values: { newStartJalali: string }) => {
    if (!rescheduleTarget) return
    try {
      await api.put(`/admin/appointments/${rescheduleTarget.id}/reschedule`, values)
      message.success('نوبت جابجا شد')
      setRescheduleTarget(null)
      rescheduleForm.resetFields()
      await load()
    } catch (error) {
      message.error(errMsg(error))
    }
  }

  const exportFiltered = async () => {
    try {
      const params = new URLSearchParams()
      if (doctorId) params.append('doctorId', doctorId)
      if (status) params.append('status', status)
      const qs = params.toString() ? `?${params.toString()}` : ''
      await downloadFile(`/admin/excel/export${qs}`, `appointments-${new Date().toISOString().slice(0, 10)}.xlsx`)
      message.success('اکسل دانلود شد')
    } catch (error) {
      message.error(errMsg(error))
    }
  }

  return (
    <div className="page-card">
      <Card title="نوبت‌ها">
        <div className="toolbar-row">
          <Select
            allowClear
            placeholder="پزشک"
            style={{ width: 220 }}
            value={doctorId}
            onChange={setDoctorId}
            options={doctors.map((d) => ({ value: d.id, label: d.fullName }))}
          />
          <Select
            allowClear
            placeholder="وضعیت"
            style={{ width: 180 }}
            value={status}
            onChange={setStatus}
            options={Object.entries(STATUS_FA).map(([value, label]) => ({ value, label }))}
          />
          <Button icon={<ReloadOutlined />} onClick={load}>
            بازخوانی
          </Button>
          <Button type="primary" icon={<DownloadOutlined />} onClick={exportFiltered} style={{ fontWeight: 700 }}>
            دانلود اکسل
          </Button>
        </div>

        <Table
          rowKey="id"
          loading={loading}
          dataSource={appointments}
          scroll={{ x: 1150 }}
          pagination={{ pageSize: 15 }}
          columns={[
            {
              title: 'زمان',
              dataIndex: 'startJalali',
              key: 'time',
              width: 170,
              sorter: (a: AppointmentDto, b: AppointmentDto) =>
                new Date(a.startsAtUtc).getTime() - new Date(b.startsAtUtc).getTime(),
              defaultSortOrder: 'ascend' as const,
              sortDirections: ['ascend', 'descend'] as const,
            },
            { title: 'پزشک', dataIndex: 'doctorName', key: 'doctor' },
            { title: 'بیمار', dataIndex: 'patientName', key: 'patient' },
            { title: 'موبایل', dataIndex: 'patientPhone', key: 'phone', width: 130 },
            { title: 'کد ملی', dataIndex: 'nationalCode', key: 'nationalCode', width: 115 },
            {
              title: 'بیمه',
              dataIndex: 'insuranceType',
              key: 'insuranceType',
              width: 100,
              render: (v: string | null) => v ? <Tag color={v === 'تکمیلی' ? 'green' : 'blue'}>{v}</Tag> : <span style={{ color: '#999' }}>-</span>,
            },
            {
              title: 'وضعیت',
              dataIndex: 'status',
              key: 'status',
              width: 140,
              render: (value: string) => <Tag color={STATUS_COLOR[value] ?? 'default'}>{STATUS_FA[value] ?? value}</Tag>,
            },
            {
              title: 'منبع',
              dataIndex: 'source',
              key: 'source',
              width: 100,
              render: (value: string) => SOURCE_FA[value] ?? value,
            },
            {
              title: 'پیگیری',
              dataIndex: 'shortCode',
              key: 'code',
              width: 110,
            },
            {
              title: 'عملیات',
              key: 'actions',
              width: 190,
              render: (_: unknown, record: AppointmentDto) =>
                record.status === 'Booked' ? (
                  <Space>
                    <Tooltip title="به بیمار پیامک لغو ارسال می‌شود">
                      <Button size="small" danger onClick={() => setCancelTarget(record)}>
                        لغو
                      </Button>
                    </Tooltip>
                    <Button size="small" onClick={() => setRescheduleTarget(record)}>
                      جابجایی
                    </Button>
                  </Space>
                ) : (
                  <span style={{ color: '#999' }}>{record.cancelReason}</span>
                ),
            },
          ]}
        />
      </Card>

      <Modal
        title="لغو نوبت"
        open={cancelTarget !== null}
        onOk={() => cancelForm.submit()}
        onCancel={() => setCancelTarget(null)}
        okText="لغو نوبت و ارسال پیامک"
        cancelText="انصراف"
        okButtonProps={{ danger: true }}
        style={{ maxWidth: '100vw' }}
        width="min(480px, 96vw)"
      >
        <Form form={cancelForm} layout="vertical" onFinish={cancel}>
          <p>
            نوبت {cancelTarget?.patientName} با {cancelTarget?.doctorName} در {cancelTarget?.startJalali}
          </p>
          <Form.Item name="reason" label="دلیل لغو (اختیاری)">
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title="جابجایی نوبت"
        open={rescheduleTarget !== null}
        onOk={() => rescheduleForm.submit()}
        onCancel={() => setRescheduleTarget(null)}
        okText="ثبت زمان جدید"
        cancelText="انصراف"
        style={{ maxWidth: '100vw' }}
        width="min(480px, 96vw)"
      >
        <Form form={rescheduleForm} layout="vertical" onFinish={reschedule}>
          <p>
            زمان فعلی: {rescheduleTarget?.startJalali} — بازه مجاز: تا دو هفته آینده و داخل ساعات کاری پزشک
          </p>
          <Form.Item
            name="newStartJalali"
            label="زمان جدید (شمسی)"
            rules={[
              { required: true },
              { pattern: /^\d{4}\/\d{2}\/\d{2} \d{2}:\d{2}$/, message: 'قالب: 1405/06/05 10:30' },
            ]}
          >
            <Input placeholder="1405/06/05 10:30" />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}
