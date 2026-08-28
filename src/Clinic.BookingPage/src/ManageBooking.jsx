import { useState } from 'react'
import { Button, Card, Descriptions, Form, Input, Result, Space, Tag, message } from 'antd'
import { confirmCancel, errMsg, lookupByCode, requestCancelOtp } from './api'
import { B } from './labels'

export default function ManageBooking() {
  const [form] = Form.useForm()
  const [appointment, setAppointment] = useState(null)
  const [otpSent, setOtpSent] = useState(false)
  const [cancelled, setCancelled] = useState(false)
  const [busy, setBusy] = useState(false)

  async function lookup(values) {
    setBusy(true)
    try {
      const dto = await lookupByCode(values.shortCode.trim().toUpperCase(), values.phone.trim())
      setAppointment(dto)
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setBusy(false)
    }
  }

  async function sendOtp() {
    if (!appointment) return
    setBusy(true)
    try {
      await requestCancelOtp(appointment.id, appointment.patientPhone)
      setOtpSent(true)
      message.success(B.otpSent)
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setBusy(false)
    }
  }

  async function doCancel(values) {
    if (!appointment) return
    setBusy(true)
    try {
      const updated = await confirmCancel(appointment.id, appointment.patientPhone, values.code.trim())
      setCancelled(true)
      setAppointment(updated)
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setBusy(false)
    }
  }

  if (cancelled) {
    return (
      <Result
        status="success"
        title={B.cancelled}
        extra={
          <Button
            onClick={() => {
              setCancelled(false)
              setOtpSent(false)
              setAppointment(null)
              form.resetFields()
            }}
          >
            {B.manageAgain}
          </Button>
        }
      />
    )
  }

  return (
    <Card title={B.manageTab}>
      <Form form={form} layout="vertical" onFinish={lookup} style={{ maxWidth: 420 }}>
        <Form.Item name="shortCode" label={B.codeLabel} rules={[{ required: true }]}>
          <Input placeholder="AB12CD34" dir="ltr" />
        </Form.Item>
        <Form.Item name="phone" label={B.phoneLabel} rules={[{ required: true }]}>
          <Input placeholder="09xxxxxxxxx" />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={busy} block>
          {B.lookup}
        </Button>
      </Form>

      {appointment && !otpSent && (
        <div style={{ marginTop: 24 }}>
          <Descriptions
            column={1}
            bordered
            size="small"
            items={[
              { key: 'name', label: B.patientName, children: appointment.patientName },
              { key: 'doctor', label: B.stepDoctor, children: appointment.doctorName },
              { key: 'time', label: B.stepTime, children: appointment.startJalali },
              {
                key: 'status',
                label: 'وضعیت',
                children: (
                  <Tag color={appointment.status === 'Booked' ? 'green' : 'orange'}>
                    {appointment.status === 'Booked' ? B.statusActive : appointment.status}
                  </Tag>
                ),
              },
            ]}
          />
          {appointment.status === 'Booked' && (
            <Button danger block style={{ marginTop: 16 }} loading={busy} onClick={sendOtp}>
              {B.cancelRequest}
            </Button>
          )}
        </div>
      )}

      {appointment && otpSent && (
        <Form form={form} layout="vertical" onFinish={doCancel} style={{ maxWidth: 420, marginTop: 24 }}>
          <p>{B.enterOtpHint}</p>
          <Form.Item name="code" label={B.otpLabel} rules={[{ required: true }]}>
            <Input placeholder="- - - - - -" dir="ltr" maxLength={6} style={{ width: 160 }} />
          </Form.Item>
          <Space>
            <Button type="primary" danger htmlType="submit" loading={busy}>
              {B.confirmCancel}
            </Button>
            <Button
              onClick={() => {
                setOtpSent(false)
              }}
            >
              بازگشت
            </Button>
          </Space>
        </Form>
      )}
    </Card>
  )
}
