import { useCallback, useEffect, useState } from 'react'
import {
  Button,
  Card,
  Empty,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  message,
} from 'antd'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { api, errMsg } from '../api'
import { WEEKDAY_FA, type DoctorAdminDto } from '../types'

interface ScheduleInput {
  weekday: number
  startTime: string
  endTime: string
}

interface DoctorPayload {
  fullName: string
  specialty?: string
  defaultVisitMinutes: number
  isActive: boolean
  schedules?: ScheduleInput[]
}

export default function Doctors() {
  const [doctors, setDoctors] = useState<DoctorAdminDto[]>([])
  const [loading, setLoading] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<DoctorAdminDto | null>(null)
  const [form] = Form.useForm<DoctorPayload & { isActive: boolean }>()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const response = await api.get<DoctorAdminDto[]>('/admin/doctors')
      setDoctors(response.data)
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldsValue({ defaultVisitMinutes: 30, isActive: true, schedules: [] })
    setModalOpen(true)
  }

  const openEdit = (doctor: DoctorAdminDto) => {
    setEditing(doctor)
    form.resetFields()
    form.setFieldsValue({
      fullName: doctor.fullName,
      specialty: doctor.specialty,
      location: doctor.location,
      defaultVisitMinutes: doctor.defaultVisitMinutes,
      isActive: doctor.isActive,
      schedules: doctor.schedules.map((s) => ({ weekday: s.weekday, startTime: s.startTime, endTime: s.endTime })),
    })
    setModalOpen(true)
  }

  const save = async () => {
    const values = await form.validateFields()
    try {
      if (editing) await api.put(`/admin/doctors/${editing.id}`, values)
      else await api.post('/admin/doctors', values)
      message.success('ذخیره شد')
      setModalOpen(false)
      await load()
    } catch (error) {
      message.error(errMsg(error))
    }
  }

  const deactivate = async (doctor: DoctorAdminDto) => {
    try {
      await api.delete(`/admin/doctors/${doctor.id}`)
      message.success('پزشک غیرفعال شد')
      await load()
    } catch (error) {
      message.error(errMsg(error))
    }
  }

  return (
    <div className="page-card">
      <Card
        title="پزشکان"
        extra={
          <Space>
            <Button icon={<ReloadOutlined />} onClick={load}>
              بازخوانی
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              افزودن پزشک
            </Button>
          </Space>
        }
      >
        <Table
          rowKey="id"
          loading={loading}
          dataSource={doctors}
          scroll={{ x: 700 }}
          pagination={false}
          columns={[
            { title: 'نام', dataIndex: 'fullName', key: 'fullName' },
            { title: 'تخصص', dataIndex: 'specialty', key: 'specialty' },
            { title: 'محل', dataIndex: 'location', key: 'location', width: 110 },
            { title: 'مدت ویزیت (دقیقه)', dataIndex: 'defaultVisitMinutes', key: 'visit', width: 130 },
            {
              title: 'وضعیت',
              dataIndex: 'isActive',
              key: 'isActive',
              width: 110,
              render: (active: boolean) => (active ? <Tag color="green">فعال</Tag> : <Tag>غیرفعال</Tag>),
            },
            {
              title: 'برنامه هفتگی',
              dataIndex: 'schedules',
              render: (_: unknown, record: DoctorAdminDto) => (
                <div style={{ minWidth: 260 }}>
                  {record.schedules.length === 0 && <span style={{ color: '#999' }}>ثبت نشده</span>}
                  {record.schedules.map((s, index) => (
                    <Tag key={index}>
                      {WEEKDAY_FA[s.weekday]} {s.startTime}-{s.endTime}
                    </Tag>
                  ))}
                </div>
              ),
            },
            {
              title: 'عملیات',
              key: 'actions',
              width: 160,
              render: (_: unknown, record: DoctorAdminDto) => (
                <Space>
                  <Button size="small" onClick={() => openEdit(record)}>
                    ویرایش
                  </Button>
                  {record.isActive && (
                    <Popconfirm title="این پزشک غیرفعال شود؟" onConfirm={() => deactivate(record)}>
                      <Button size="small" danger>
                        حذف
                      </Button>
                    </Popconfirm>
                  )}
                </Space>
              ),
            },
          ]}
        />
      </Card>

      <Modal
        title={editing ? 'ویرایش پزشک' : 'افزودن پزشک'}
        open={modalOpen}
        onOk={save}
        onCancel={() => setModalOpen(false)}
        okText="ذخیره"
        cancelText="انصراف"
        width="min(640px, 96vw)"
        style={{ maxWidth: '100vw' }}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item name="fullName" label="نام و نام خانوادگی" rules={[{ required: true, message: 'نام الزامی است' }]}>
            <Input placeholder="دکتر ..." />
          </Form.Item>
          <Form.Item name="specialty" label="تخصص / حرفه">
            <Input />
          </Form.Item>
          <Form.Item name="location" label="محل (مثال: طبقه اول، زیرزمین)">
            <Input placeholder="طبقه اول" />
          </Form.Item>
          <Form.Item name="defaultVisitMinutes" label="مدت پیش‌فرض هر ویزیت (دقیقه)" rules={[{ required: true }]}>
            <InputNumber min={5} max={240} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="isActive" label="فعال" valuePropName="checked">
            <Switch />
          </Form.Item>

          <Form.List name="schedules">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <Space key={field.key} align="baseline" style={{ display: 'flex' }}>
                    <Form.Item
                      name={[field.name, 'weekday']}
                      rules={[{ required: true, message: 'روز' }]}
                      style={{ width: 140 }}
                    >
                      <Select options={WEEKDAY_FA.map((label, weekday) => ({ value: weekday, label }))} />
                    </Form.Item>
                    <Form.Item
                      name={[field.name, 'startTime']}
                      rules={[
                        { required: true },
                        { pattern: /^\d{2}:\d{2}$/, message: 'HH:mm' },
                      ]}
                      style={{ width: 110 }}
                    >
                      <Input placeholder="09:00" />
                    </Form.Item>
                    <Form.Item
                      name={[field.name, 'endTime']}
                      rules={[
                        { required: true },
                        { pattern: /^\d{2}:\d{2}$/, message: 'HH:mm' },
                      ]}
                      style={{ width: 110 }}
                    >
                      <Input placeholder="13:00" />
                    </Form.Item>
                    <Button type="link" danger onClick={() => remove(field.name)}>
                      حذف ردیف
                    </Button>
                  </Space>
                ))}
                <Button type="dashed" block onClick={() => add({ startTime: '09:00', endTime: '13:00' })}>
                  افزودن بازه کاری
                </Button>
              </>
            )}
          </Form.List>
        </Form>
      </Modal>
    </div>
  )
}
