import { Tabs } from 'antd'
import { useState } from 'react'
import BookingWizard from '../BookingWizard'
import ManageBooking from '../ManageBooking'

export default function AppointmentPage() {
  const [tab, setTab] = useState('book')

  return (
    <>
      <header className="hero">
        <div className="hero-badge">شبانه‌روزی · ۲۴ ساعته</div>
        <h1>رزرو نوبت آنلاین</h1>
        <p>بدون تماس تلفنی و صف انتظار — نوبت خود را در چند ثانیه رزرو کنید.</p>
      </header>

      <main className="page-main">
        <div className="content-wrap">
          <Tabs
            className="booking-tabs"
            activeKey={tab}
            onChange={setTab}
            items={[
              { key: 'book', label: 'رزرو نوبت جدید', children: <BookingWizard /> },
              { key: 'manage', label: 'مدیریت نوبت من', children: <ManageBooking /> },
            ]}
          />
        </div>
      </main>
    </>
  )
}
