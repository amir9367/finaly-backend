import { useState } from 'react'
import { Button, Form, Input, message } from 'antd'
import { api, errMsg, setToken } from '../api'

export default function Login({ onSuccess }: { onSuccess: () => void }) {
  const [loading, setLoading] = useState(false)

  const onFinish = async (values: { username: string; password: string }) => {
    setLoading(true)
    try {
      const response = await api.post('/admin/auth/login', values)
      setToken(response.data.accessToken)
      message.success('خوش آمدید')
      onSuccess()
    } catch (error) {
      message.error(errMsg(error))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="login-page">
      <div className="login-card">
        <div className="login-logo">
          <div className="login-logo-name">درمانگاه طب الرضا(ع)</div>
          <div className="login-logo-sub">ورود به پنل مدیریت</div>
        </div>
        <Form layout="vertical" onFinish={onFinish}>
          <Form.Item name="username" label="نام کاربری" rules={[{ required: true, message: 'نام کاربری را وارد کنید' }]}>
            <Input autoFocus size="large" />
          </Form.Item>
          <Form.Item name="password" label="گذرواژه" rules={[{ required: true, message: 'گذرواژه را وارد کنید' }]}>
            <Input.Password size="large" />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={loading} size="large"
            style={{ background: 'var(--gm)', borderColor: 'var(--gm)', marginTop: 4 }}>
            ورود
          </Button>
          <div className="login-footnote">درمانگاه شبانه‌روزی طب الرضا(ع) — وقت‌دهی: ۳۴۲۴۶</div>
        </Form>
      </div>
    </div>
  )
}
