import { useState, useEffect } from 'react'
import { Popconfirm } from 'antd'
import {
  CalendarOutlined,
  DashboardOutlined,
  FileExcelOutlined,
  LogoutOutlined,
  MenuOutlined,
  MessageOutlined,
  TeamOutlined,
  CloseOutlined,
} from '@ant-design/icons'
import { L } from './labels'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Doctors from './pages/Doctors'
import AppointmentsPage from './pages/AppointmentsPage'
import ExcelPage from './pages/ExcelPage'
import SmsLogs from './pages/SmsLogs'
import { getToken, setToken } from './api'

type PageKey = 'dashboard' | 'doctors' | 'appointments' | 'excel' | 'sms'

const NAV = [
  { key: 'dashboard', icon: <DashboardOutlined />, label: L.dashboard },
  { key: 'doctors', icon: <TeamOutlined />, label: L.doctors },
  { key: 'appointments', icon: <CalendarOutlined />, label: L.appointments },
  { key: 'excel', icon: <FileExcelOutlined />, label: L.excel },
  { key: 'sms', icon: <MessageOutlined />, label: L.sms },
]

function SidebarContent({ page, onNav, onLogout }: {
  page: PageKey
  onNav: (k: PageKey) => void
  onLogout: () => void
}) {
  return (
    <>
      <div className="sidebar-brand">
        <div className="sidebar-brand-name">{L.appTitle}</div>
        <div className="sidebar-brand-sub">{L.appSubtitle}</div>
      </div>
      <nav className="sidebar-nav">
        {NAV.map(n => (
          <button
            key={n.key}
            className={`nav-item${page === n.key ? ' active' : ''}`}
            onClick={() => onNav(n.key as PageKey)}
          >
            <span className="nav-icon">{n.icon}</span>
            {n.label}
          </button>
        ))}
      </nav>
      <div className="sidebar-footer">
        <Popconfirm title={L.logoutConfirm} onConfirm={onLogout}>
          <button>
            <LogoutOutlined />
            {L.logout}
          </button>
        </Popconfirm>
      </div>
    </>
  )
}

export default function App() {
  const [token, setTokenState] = useState<string | null>(getToken())
  const [page, setPage] = useState<PageKey>('dashboard')
  const [drawerOpen, setDrawerOpen] = useState(false)

  useEffect(() => {
    if (drawerOpen) document.body.style.overflow = 'hidden'
    else document.body.style.overflow = ''
    return () => { document.body.style.overflow = '' }
  }, [drawerOpen])

  if (!token) return <Login onSuccess={() => setTokenState(getToken())} />

  const handleLogout = () => { setToken(null); setTokenState(null) }
  const handleNav = (k: PageKey) => { setPage(k); setDrawerOpen(false) }

  const page_content = (
    <>
      {page === 'dashboard' && <Dashboard />}
      {page === 'doctors' && <Doctors />}
      {page === 'appointments' && <AppointmentsPage />}
      {page === 'excel' && <ExcelPage />}
      {page === 'sms' && <SmsLogs />}
    </>
  )

  return (
    <div className="app-shell">
      {/* Desktop sidebar */}
      <aside className="sidebar">
        <SidebarContent page={page} onNav={handleNav} onLogout={handleLogout} />
      </aside>

      {/* Mobile topbar */}
      <div className="topbar">
        <button className="topbar-btn" onClick={() => setDrawerOpen(true)}>
          <MenuOutlined />
        </button>
        <span className="topbar-title">{L.appTitle}</span>
        <span style={{ width: 32 }} />
      </div>

      {/* Mobile drawer */}
      <div className={`drawer-overlay${drawerOpen ? ' open' : ''}`} onClick={() => setDrawerOpen(false)} />
      <aside className={`drawer${drawerOpen ? ' open' : ''}`}>
        <div style={{ display: 'flex', justifyContent: 'flex-start', padding: '12px 12px 0' }}>
          <button className="topbar-btn" style={{ color: 'rgba(255,255,255,0.7)' }} onClick={() => setDrawerOpen(false)}>
            <CloseOutlined />
          </button>
        </div>
        <SidebarContent page={page} onNav={handleNav} onLogout={handleLogout} />
      </aside>

      {/* Page content */}
      <main className="main-content">
        <div className="admin-topbar">
          <div className="admin-search">
            <span className="admin-search-icon">⌕</span>
            <input placeholder="جستجو در نوبت‌ها، پزشکان..." disabled />
          </div>
          <div className="admin-topbar-actions">
            <button className="admin-icon-btn" title="اعلان‌ها">◐</button>
            <button className="admin-icon-btn" title="تنظیمات">⚙</button>
            <div className="admin-user">
              <div style={{ width: 36, height: 36, borderRadius: '50%', background: 'var(--accent)', color: '#171717', display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 800, fontSize: '0.85rem', flexShrink: 0 }}>م</div>
              <div className="admin-user-meta">
                <b>مدیر کلینیک</b>
                <span>Admin</span>
              </div>
            </div>
          </div>
        </div>
        {page_content}
      </main>
    </div>
  )
}
