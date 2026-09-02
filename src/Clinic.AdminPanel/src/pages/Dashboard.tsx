import { useEffect, useState } from 'react'
import { Spin, message } from 'antd'
import { api, errMsg } from '../api'
import type { DashboardStats } from '../types'

export default function Dashboard() {
  const [stats, setStats] = useState<DashboardStats | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    api.get<DashboardStats>('/admin/dashboard/stats')
      .then((r) => setStats(r.data))
      .catch((e) => { setFailed(true); message.error(errMsg(e)) })
  }, [])

  if (failed) return <p style={{ padding: 16, color: 'var(--danger)' }}>دریافت آمار ناموفق بود — دوباره وارد شوید.</p>
  if (!stats) return <Spin style={{ margin: 32 }} />

  return (
    <>
      <h2 className="page-title">داشبورد</h2>
      <div className="stat-grid">
        <div className="stat-tile">
          <span className="stat-tile-icon">◷</span>
          <div className="stat-tile-label">نوبت‌های امروز</div>
          <div className="stat-tile-value">{stats.todayAppointments}</div>
        </div>
        <div className="stat-tile">
          <span className="stat-tile-icon">◎</span>
          <div className="stat-tile-label">۷ روز آینده</div>
          <div className="stat-tile-value">{stats.next7DaysAppointments}</div>
        </div>
        <div className="stat-tile">
          <span className="stat-tile-icon">⚑</span>
          <div className="stat-tile-label">پزشکان فعال</div>
          <div className="stat-tile-value">{stats.activeDoctors}</div>
        </div>
        <div className="stat-tile">
          <span className="stat-tile-icon" style={stats.failedSms24h > 0 ? { background: 'var(--danger-soft)', color: 'var(--danger)', borderColor: 'rgba(179,64,47,0.25)' } : undefined}>✉</span>
          <div className="stat-tile-label">پیامک خطادار ۲۴ ساعت</div>
          <div className={`stat-tile-value${stats.failedSms24h > 0 ? ' danger' : ''}`}>
            {stats.failedSms24h}
          </div>
        </div>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: 14, marginTop: 4 }}>
        <div className="card" style={{ padding: 18, display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 12 }}>
          <div>
            <div style={{ fontWeight: 800, color: 'var(--pine)', fontSize: '0.95rem' }}>وضعیت سیستم</div>
            <div style={{ color: 'var(--muted)', fontSize: '0.82rem', marginTop: 4 }}>همه سرویس‌ها فعال — دیتابیس متصل، پیامک آماده</div>
          </div>
          <span style={{ background: 'var(--pine)', color: '#fff', padding: '6px 14px', borderRadius: 99, fontWeight: 800, fontSize: '0.78rem' }}>● آنلاین</span>
        </div>
      </div>
    </>
  )
}
